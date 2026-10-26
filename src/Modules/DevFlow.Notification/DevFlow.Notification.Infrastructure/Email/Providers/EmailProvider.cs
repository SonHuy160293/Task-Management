using DevFlow.AspNetCore.HttpClient.Abstractions;
using Microsoft.Extensions.Options;

namespace DevFlow.Notification.Infrastructure.Email.Providers;
public sealed record ProviderResult(string Status, string? ProviderId = null, string? Code = null, TimeSpan? RetryAfter = null);
public interface IEmailProvider
{
    Task<ProviderResult> SendAsync(Guid id, string frozenJson, CancellationToken cancellationToken);
}

public sealed class EmailProvider(IIntegrationHttpClient<IEmailProvider> client, IOptions<EmailDeliveryOptions> settings) : IEmailProvider
{
    public async Task<ProviderResult> SendAsync(Guid id, string frozenJson, CancellationToken ct)
    {
        if (settings.Value.Mode != "Resend")
        {
            return new("Paused", Code: "DeliveryDisabled");
        }

        var result = await client.PostExternalJsonAsync<ResendResponse>("emails", frozenJson, "devflow/email/" + id.ToString("D"), ct);
        if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Data?.Id))
        {
            return new("Accepted", result.Data.Id);
        }

        int status = (int? )result.StatusCode ?? 0;
        if (status is 401 or 403)
        {
            return new("Paused", Code: "ProviderConfiguration");
        }

        if (status is >= 400 and < 500 && status is not 408 and not 429 && status != 409)
        {
            return new("Permanent", Code: "ProviderRejected");
        }

        return new("Retry", Code: "ProviderUnavailable", RetryAfter: result.RetryAfter);
    }

    private sealed record ResendResponse(string Id);
}
