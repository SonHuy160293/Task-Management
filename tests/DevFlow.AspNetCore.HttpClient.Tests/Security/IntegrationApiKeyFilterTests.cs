using DevFlow.AspNetCore.Options;
using DevFlow.AspNetCore.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace DevFlow.AspNetCore.HttpClient.Tests.Security;

public sealed class IntegrationApiKeyFilterTests
{
    [Fact]
    public async Task FilterAcceptsValidRotatedKey()
    {
        AuthorizationFilterContext context = CreateContext("new-key");
        var filter = CreateFilter("old-key", "new-key");

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task FilterRejectsMissingInvalidDuplicateAndOversizedHeaders()
    {
        foreach (string[] values in new[]
        {
            Array.Empty<string>(),
            ["wrong-key"],
            ["valid-key", "valid-key"],
            [new string('x', 4097)],
        })
        {
            AuthorizationFilterContext context = CreateContext(values);
            IntegrationApiKeyFilter filter = CreateFilter("valid-key");

            await filter.OnAuthorizationAsync(context);

            ObjectResult result = Assert.IsType<ObjectResult>(context.Result);
            Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        }
    }

    private static IntegrationApiKeyFilter CreateFilter(params string[] keys)
        => new(Microsoft.Extensions.Options.Options.Create(new IntegrationApiOptions
        {
            HeaderName = "X-Service-Key",
            ApiKeys = keys,
        }));

    private static AuthorizationFilterContext CreateContext(params string[] values)
    {
        var httpContext = new DefaultHttpContext();
        if (values.Length > 0)
        {
            httpContext.Request.Headers.Append("X-Service-Key", values);
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, []);
    }
}
