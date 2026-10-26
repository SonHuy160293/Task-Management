using DevFlow.SharedKernel.Abstractions.Exceptions;
using DevFlow.SharedKernel.Abstractions.Message;
using DevFlow.SharedKernel.Abstractions.Shared;
using DevFlow.SharedKernel.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ValidationException = DevFlow.SharedKernel.Exceptions.ValidationException;

namespace DevFlow.AspNetCore.Middleware;

public sealed class GlobalExceptionHandlingMiddleware
{
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private readonly RequestDelegate _next;

    public GlobalExceptionHandlingMiddleware(ILogger<GlobalExceptionHandlingMiddleware> logger, RequestDelegate next)
    {
        _logger = logger;
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            LogException(exception);
            await WriteErrorResponseAsync(context, exception);
        }
    }

    private void LogException(Exception exception)
    {
        if (exception is DomainException or CqrsValidationException)
        {
            _logger.LogWarning(exception, "{Message}", exception.Message);
            return;
        }

        _logger.LogError(exception, "{Message}", exception.Message);
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, Exception exception)
    {
        var statusCode = GetStatusCode(exception);
        var response = ApiResponse.Fail(CreateResponse(context, exception));

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(response);
    }

    private static ApiErrorResponse CreateResponse(HttpContext context, Exception exception)
        => exception switch
        {
            ValidationException validationException => new ApiErrorResponse(
                validationException.Code,
                validationException.Message,
                ToValidationErrors(validationException.Errors),
                context.TraceIdentifier),

            CqrsValidationException validationException => new ApiErrorResponse(
                "Validation.Failure",
                "One or more validation errors occurred.",
                validationException.Errors.ToDictionary(error => error, error => new[] { error }),
                context.TraceIdentifier),

            DomainException domainException => new ApiErrorResponse(
                domainException.Code,
                domainException.Message,
                TraceId: context.TraceIdentifier),

            DbUpdateConcurrencyException => new ApiErrorResponse(
                "Database.Concurrency",
                "The data was modified by another request. Please reload and try again.",
                TraceId: context.TraceIdentifier),

            DbUpdateException => new ApiErrorResponse(
                "Database.UpdateFailed",
                "The database update failed.",
                TraceId: context.TraceIdentifier),

            _ => new ApiErrorResponse(
                "Server.Unhandled",
                "An unexpected error occurred.",
                TraceId: context.TraceIdentifier)
        };

    private static int GetStatusCode(Exception exception)
        => exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            CqrsValidationException => StatusCodes.Status400BadRequest,
            BadRequestException => StatusCodes.Status400BadRequest,
            NotFoundException => StatusCodes.Status404NotFound,
            ForbiddenException => StatusCodes.Status403Forbidden,
            ConflictException => StatusCodes.Status409Conflict,
            UnauthorizedException => StatusCodes.Status401Unauthorized,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            ServiceUnavailableException => StatusCodes.Status503ServiceUnavailable,
            DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
            DomainException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

    private static IReadOnlyDictionary<string, string[]> ToValidationErrors(IReadOnlyCollection<ValidationError> errors)
        => errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());
}
