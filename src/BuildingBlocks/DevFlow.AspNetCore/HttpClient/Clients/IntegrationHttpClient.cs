using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevFlow.AspNetCore.HttpClient.Abstractions;
using DevFlow.AspNetCore.HttpClient.Serialization;
using DevFlow.SharedKernel.Integrations;

namespace DevFlow.AspNetCore.HttpClient.Clients;

public sealed class IntegrationHttpClient<TDestination>(System.Net.Http.HttpClient client)
    : IIntegrationHttpClient<TDestination>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<IntegrationResult<TResponse>> PostExternalJsonAsync<TResponse>(
        string endpoint, string json, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (!TryCreateRelativeUri(endpoint, out Uri? uri) || idempotencyKey.Length is < 1 or > 128
            || idempotencyKey.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '/' and not '-'))
        {
            throw new ArgumentException("Invalid external request metadata.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;
                if (retryAfter is null && response.Headers.RetryAfter?.Date is { } date)
                {
                    retryAfter = date - DateTimeOffset.UtcNow;
                }

                return IntegrationResult<TResponse>.Failure(response.StatusCode,
                    new IntegrationError("Integration.ExternalFailure", "External request rejected.", IntegrationFailureCategory.Downstream))
                    .WithRetryAfter(retryAfter);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var body = new MemoryStream();
            byte[] chunk = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (body.Length + read > 65536) throw new JsonException("External response too large.");
                body.Write(chunk, 0, read);
            }

            var data = JsonSerializer.Deserialize<TResponse>(body.GetBuffer().AsSpan(0, (int)body.Length), SerializerOptions);
            return data is null ? Failure<TResponse>(response.StatusCode, "Integration.InvalidJson", "Invalid external response.", IntegrationFailureCategory.InvalidResponse)
                : IntegrationResult<TResponse>.Success(data, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            return Failure<TResponse>(null, "Integration.NetworkFailure", "External transport unavailable.", IntegrationFailureCategory.Network);
        }
        catch (JsonException)
        {
            return Failure<TResponse>(null, "Integration.InvalidJson", "Invalid external response.", IntegrationFailureCategory.InvalidResponse);
        }
        catch (Polly.Timeout.TimeoutRejectedException)
        {
            return Failure<TResponse>(null, "Integration.Timeout", "External transport timed out.", IntegrationFailureCategory.Timeout);
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException)
        {
            return Failure<TResponse>(null, "Integration.CircuitOpen", "External transport paused.", IntegrationFailureCategory.CircuitOpen);
        }
    }

    public Task<IntegrationResult<TResponse>> GetAsync<TResponse>(
        string endpoint,
        CancellationToken cancellationToken = default)
        => SendAsync<TResponse>(HttpMethod.Get, endpoint, null, cancellationToken);

    public Task<IntegrationResult<TResponse>> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<TResponse>(HttpMethod.Post, endpoint, JsonContent.Create(request, options: SerializerOptions), cancellationToken);

    public Task<IntegrationResult<TResponse>> PutAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<TResponse>(HttpMethod.Put, endpoint, JsonContent.Create(request, options: SerializerOptions), cancellationToken);

    public async Task<IntegrationResult> DeleteAsync(
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        IntegrationResult<NoContent> result = await SendAsync<NoContent>(
            HttpMethod.Delete,
            endpoint,
            null,
            cancellationToken,
            allowEmpty: true);

        return result.IsSuccess
            ? IntegrationResult.Success(result.StatusCode ?? HttpStatusCode.NoContent)
            : IntegrationResult.Failure(result.StatusCode, result.Error!);
    }

    private async Task<IntegrationResult<TResponse>> SendAsync<TResponse>(
        HttpMethod method,
        string endpoint,
        HttpContent? content,
        CancellationToken cancellationToken,
        bool allowEmpty = false)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (!TryCreateRelativeUri(endpoint, out Uri? requestUri))
        {
            content?.Dispose();
            return Failure<TResponse>(null, "Integration.InvalidEndpoint", "The integration endpoint is invalid.", IntegrationFailureCategory.InvalidResponse);
        }

        using var request = new HttpRequestMessage(method, requestUri) { Content = content };
        try
        {
            using HttpResponseMessage response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return await CreateHttpFailureAsync<TResponse>(response, cancellationToken);
            }

            if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
            {
                return allowEmpty || typeof(TResponse) == typeof(NoContent)
                    ? IntegrationResult<TResponse>.Success(default!, response.StatusCode)
                    : Failure<TResponse>(response.StatusCode, "Integration.EmptyResponse", "The dependency returned an empty response.", IntegrationFailureCategory.InvalidResponse);
            }

            try
            {
                await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                IntegrationResponseEnvelope<TResponse>? envelope = await JsonSerializer.DeserializeAsync<IntegrationResponseEnvelope<TResponse>>(
                    stream,
                    SerializerOptions,
                    cancellationToken);

                if (envelope is null || !envelope.Success || envelope.Data is null)
                {
                    return Failure<TResponse>(response.StatusCode, "Integration.InvalidResponse", "The dependency returned an invalid response.", IntegrationFailureCategory.InvalidResponse);
                }

                return IntegrationResult<TResponse>.Success(envelope.Data, response.StatusCode);
            }
            catch (JsonException)
            {
                return Failure<TResponse>(response.StatusCode, "Integration.InvalidJson", "The dependency returned malformed JSON.", IntegrationFailureCategory.InvalidResponse);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Polly.Timeout.TimeoutRejectedException)
        {
            return Failure<TResponse>(null, "Integration.Timeout", "The dependency did not respond within the configured time limit.", IntegrationFailureCategory.Timeout);
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException)
        {
            return Failure<TResponse>(null, "Integration.CircuitOpen", "The dependency is temporarily unavailable.", IntegrationFailureCategory.CircuitOpen);
        }
        catch (HttpRequestException)
        {
            return Failure<TResponse>(null, "Integration.NetworkFailure", "The dependency could not be reached.", IntegrationFailureCategory.Network);
        }
    }

    private static async Task<IntegrationResult<TResponse>> CreateHttpFailureAsync<TResponse>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        IntegrationResponseError? error = null;
        try
        {
            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            IntegrationResponseEnvelope<JsonElement>? envelope = await JsonSerializer.DeserializeAsync<IntegrationResponseEnvelope<JsonElement>>(
                stream,
                SerializerOptions,
                cancellationToken);
            error = envelope?.Error;
        }
        catch (JsonException)
        {
            // Downstream bodies are intentionally not exposed when their error envelope is malformed.
        }

        IntegrationFailureCategory category = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => IntegrationFailureCategory.Validation,
            HttpStatusCode.Unauthorized => IntegrationFailureCategory.Authentication,
            HttpStatusCode.Forbidden => IntegrationFailureCategory.Authorization,
            HttpStatusCode.NotFound => IntegrationFailureCategory.NotFound,
            HttpStatusCode.Conflict => IntegrationFailureCategory.Conflict,
            HttpStatusCode.TooManyRequests => IntegrationFailureCategory.RateLimited,
            HttpStatusCode.RequestTimeout => IntegrationFailureCategory.Timeout,
            _ => IntegrationFailureCategory.Downstream,
        };

        string code = string.IsNullOrWhiteSpace(error?.Code) ? $"Integration.Http{(int)response.StatusCode}" : error.Code;
        string message = string.IsNullOrWhiteSpace(error?.Message) ? "The dependency rejected the request." : error.Message;
        return IntegrationResult<TResponse>.Failure(
            response.StatusCode,
            new IntegrationError(code, message, category, error?.Errors));
    }

    private static bool TryCreateRelativeUri(string endpoint, out Uri? uri)
    {
        uri = null;
        return !string.IsNullOrWhiteSpace(endpoint)
            && !endpoint.StartsWith("//", StringComparison.Ordinal)
            && Uri.TryCreate(endpoint, UriKind.Relative, out uri);
    }

    private static IntegrationResult<T> Failure<T>(
        HttpStatusCode? statusCode,
        string code,
        string message,
        IntegrationFailureCategory category)
        => IntegrationResult<T>.Failure(statusCode, new IntegrationError(code, message, category));

    private sealed record NoContent;
}
