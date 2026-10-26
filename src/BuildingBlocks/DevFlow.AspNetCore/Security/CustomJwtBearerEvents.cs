using System.Security.Claims;
using DevFlow.SharedKernel.Abstractions.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DevFlow.AspNetCore.Security;

public sealed class CustomJwtBearerEvents : JwtBearerEvents
{
    public override async Task Challenge(JwtBearerChallengeContext context)
    {
        context.HandleResponse();
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(new ApiErrorResponse(
            "Request.Unauthorized",
            "Authentication is required.",
            TraceId: context.HttpContext.TraceIdentifier)));
    }

    public override async Task Forbidden(ForbiddenContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(new ApiErrorResponse(
            "Request.Forbidden",
            "Access is forbidden.",
            TraceId: context.HttpContext.TraceIdentifier)));
    }

    public override Task TokenValidated(TokenValidatedContext context)
    {
        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrWhiteSpace(userId))
        {
            context.Fail("Missing user identifier.");
        }

        return Task.CompletedTask;
    }
}
