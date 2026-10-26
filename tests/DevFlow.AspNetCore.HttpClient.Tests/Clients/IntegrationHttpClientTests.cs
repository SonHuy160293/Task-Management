using System.Net;
using System.Text;
using DevFlow.AspNetCore.HttpClient.Clients;
using DevFlow.SharedKernel.Integrations;

namespace DevFlow.AspNetCore.HttpClient.Tests.Clients;

public sealed class IntegrationHttpClientTests
{
    [Fact]
    public async Task GetAsyncDeserializesSuccessfulEnvelope()
    {
        using var client = CreateClient(_ => JsonResponse(HttpStatusCode.OK, """
            {"success":true,"data":{"name":"Ada"},"error":null}
            """));

        IntegrationResult<TestResponse> result = await new IntegrationHttpClient<TestResponse>(client).GetAsync<TestResponse>("users/1");

        Assert.True(result.IsSuccess);
        Assert.Equal("Ada", result.Data?.Name);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    [Fact]
    public async Task PostAndPutAsyncSerializeJsonRequests()
    {
        var requests = new List<(HttpMethod Method, string Body)>();
        using var client = CreateClient(async (request, cancellationToken) =>
        {
            requests.Add((request.Method, await request.Content!.ReadAsStringAsync(cancellationToken)));
            return JsonResponse(HttpStatusCode.OK, """{"success":true,"data":{"name":"saved"},"error":null}""");
        });

        await new IntegrationHttpClient<TestResponse>(client).PostAsync<TestRequest, TestResponse>("users", new TestRequest("post"));
        await new IntegrationHttpClient<TestResponse>(client).PutAsync<TestRequest, TestResponse>("users/1", new TestRequest("put"));

        Assert.Collection(
            requests,
            item =>
            {
                Assert.Equal(HttpMethod.Post, item.Method);
                Assert.Contains("\"name\":\"post\"", item.Body, StringComparison.Ordinal);
            },
            item =>
            {
                Assert.Equal(HttpMethod.Put, item.Method);
                Assert.Contains("\"name\":\"put\"", item.Body, StringComparison.Ordinal);
            });
    }

    [Fact]
    public async Task DeleteAsyncAcceptsNoContent()
    {
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        IntegrationResult result = await new IntegrationHttpClient<TestResponse>(client).DeleteAsync("users/1");

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, IntegrationFailureCategory.Validation)]
    [InlineData(HttpStatusCode.Unauthorized, IntegrationFailureCategory.Authentication)]
    [InlineData(HttpStatusCode.Forbidden, IntegrationFailureCategory.Authorization)]
    [InlineData(HttpStatusCode.NotFound, IntegrationFailureCategory.NotFound)]
    [InlineData(HttpStatusCode.Conflict, IntegrationFailureCategory.Conflict)]
    [InlineData(HttpStatusCode.TooManyRequests, IntegrationFailureCategory.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, IntegrationFailureCategory.Downstream)]
    public async Task GetAsyncMapsHttpFailures(
        HttpStatusCode statusCode,
        IntegrationFailureCategory expectedCategory)
    {
        using var client = CreateClient(_ => JsonResponse(statusCode, """
            {"success":false,"data":null,"error":{"code":"Downstream.Error","message":"Safe failure"}}
            """));

        IntegrationResult<TestResponse> result = await new IntegrationHttpClient<TestResponse>(client).GetAsync<TestResponse>("users/1");

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCategory, result.Error?.Category);
        Assert.Equal("Downstream.Error", result.Error?.Code);
        Assert.Equal("Safe failure", result.Error?.Message);
    }

    [Theory]
    [InlineData("not-json", "Integration.InvalidJson")]
    [InlineData("", "Integration.EmptyResponse")]
    public async Task GetAsyncRejectsInvalidSuccessfulResponses(string body, string expectedCode)
    {
        using var client = CreateClient(_ => JsonResponse(HttpStatusCode.OK, body));

        IntegrationResult<TestResponse> result = await new IntegrationHttpClient<TestResponse>(client).GetAsync<TestResponse>("users/1");

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error?.Code);
        Assert.Equal(IntegrationFailureCategory.InvalidResponse, result.Error?.Category);
    }

    [Fact]
    public async Task GetAsyncMapsNetworkFailure()
    {
        using var client = CreateClient(_ => throw new HttpRequestException("socket failure"));

        IntegrationResult<TestResponse> result = await new IntegrationHttpClient<TestResponse>(client).GetAsync<TestResponse>("users/1");

        Assert.Equal(IntegrationFailureCategory.Network, result.Error?.Category);
    }

    [Fact]
    public async Task GetAsyncPropagatesCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var client = CreateClient((_, token) => Task.FromCanceled<HttpResponseMessage>(token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new IntegrationHttpClient<TestResponse>(client).GetAsync<TestResponse>("users/1", cancellation.Token));
    }

    [Fact]
    public async Task GetAsyncRejectsAbsoluteEndpoint()
    {
        var sent = false;
        using var client = CreateClient(_ =>
        {
            sent = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        IntegrationResult<TestResponse> result = await new IntegrationHttpClient<TestResponse>(client).GetAsync<TestResponse>("https://attacker.invalid/users");

        Assert.False(sent);
        Assert.Equal("Integration.InvalidEndpoint", result.Error?.Code);
    }

    private static System.Net.Http.HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => CreateClient((request, _) => Task.FromResult(responder(request)));

    private static System.Net.Http.HttpClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        => new(new DelegateHandler(responder)) { BaseAddress = new Uri("https://identity.test/") };

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
        => new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => responder(request, cancellationToken);
    }

    private sealed record TestRequest(string Name);

    private sealed record TestResponse(string Name);
}
