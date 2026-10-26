using System.Reflection;
using DevFlow.SharedKernel.Abstractions.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DevFlow.AspNetCore.Filters;

public sealed class ApiResponseEnvelopeFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        context.Result = context.Result switch
        {
            ObjectResult objectResult => WrapObjectResult(context, objectResult),
            StatusCodeResult statusCodeResult => WrapStatusCodeResult(context, statusCodeResult),
            EmptyResult => new OkObjectResult(ApiResponse.Ok()),
            _ => context.Result
        };
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }

    private static IActionResult WrapObjectResult(ResultExecutingContext context, ObjectResult result)
    {
        if (result.Value is ApiResponse)
        {
            return result;
        }

        var statusCode = result.StatusCode ?? InferStatusCode(result);

        result.Value = statusCode >= StatusCodes.Status400BadRequest
            ? ApiResponse.Fail(CreateError(context, statusCode, result.Value))
            : ApiResponse.Ok(result.Value);
        result.StatusCode = statusCode;

        return result;
    }

    private static IActionResult WrapStatusCodeResult(ResultExecutingContext context, StatusCodeResult result)
    {
        if (result.StatusCode == StatusCodes.Status204NoContent)
        {
            return new OkObjectResult(ApiResponse.Ok());
        }

        if (result.StatusCode >= StatusCodes.Status400BadRequest)
        {
            return new ObjectResult(ApiResponse.Fail(CreateError(context, result.StatusCode, null)))
            {
                StatusCode = result.StatusCode
            };
        }

        return new ObjectResult(ApiResponse.Ok())
        {
            StatusCode = result.StatusCode
        };
    }

    private static int InferStatusCode(ObjectResult result)
        => result switch
        {
            CreatedAtActionResult => StatusCodes.Status201Created,
            CreatedAtRouteResult => StatusCodes.Status201Created,
            BadRequestObjectResult => StatusCodes.Status400BadRequest,
            UnauthorizedObjectResult => StatusCodes.Status401Unauthorized,
            NotFoundObjectResult => StatusCodes.Status404NotFound,
            ConflictObjectResult => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status200OK
        };

    private static ApiErrorResponse CreateError(ResultExecutingContext context, int statusCode, object? value)
    {
        if (value is ValidationProblemDetails validationProblemDetails)
        {
            return new ApiErrorResponse(
                "Validation.Failure",
                validationProblemDetails.Title ?? "One or more validation errors occurred.",
                validationProblemDetails.Errors.ToDictionary(
                    item => item.Key,
                    item => item.Value),
                context.HttpContext.TraceIdentifier);
        }

        if (value is ProblemDetails problemDetails)
        {
            return new ApiErrorResponse(
                GetDefaultCode(statusCode),
                problemDetails.Title ?? problemDetails.Detail ?? GetDefaultMessage(statusCode),
                TraceId: context.HttpContext.TraceIdentifier);
        }

        if (value is ApiErrorResponse apiError)
        {
            return apiError with { TraceId = apiError.TraceId ?? context.HttpContext.TraceIdentifier };
        }

        if (value is Error error)
        {
            return new ApiErrorResponse(
                error.Code,
                error.Message,
                TraceId: context.HttpContext.TraceIdentifier);
        }

        var message = TryReadMessage(value) ?? GetDefaultMessage(statusCode);

        return new ApiErrorResponse(
            GetDefaultCode(statusCode),
            message,
            TraceId: context.HttpContext.TraceIdentifier);
    }

    private static string? TryReadMessage(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is string message)
        {
            return message;
        }

        return value.GetType()
            .GetProperty("message", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
            ?.GetValue(value)
            ?.ToString();
    }

    private static string GetDefaultCode(int statusCode)
        => statusCode switch
        {
            StatusCodes.Status400BadRequest => "Request.BadRequest",
            StatusCodes.Status401Unauthorized => "Request.Unauthorized",
            StatusCodes.Status403Forbidden => "Request.Forbidden",
            StatusCodes.Status404NotFound => "Request.NotFound",
            StatusCodes.Status405MethodNotAllowed => "Request.MethodNotAllowed",
            StatusCodes.Status409Conflict => "Request.Conflict",
            StatusCodes.Status415UnsupportedMediaType => "Request.UnsupportedMediaType",
            _ => "Request.Failed"
        };

    private static string GetDefaultMessage(int statusCode)
        => statusCode switch
        {
            StatusCodes.Status400BadRequest => "The request is invalid.",
            StatusCodes.Status401Unauthorized => "Authentication is required.",
            StatusCodes.Status403Forbidden => "Access is forbidden.",
            StatusCodes.Status404NotFound => "The resource was not found.",
            StatusCodes.Status405MethodNotAllowed => "The HTTP method is not allowed for this endpoint.",
            StatusCodes.Status409Conflict => "The request conflicts with the current state.",
            StatusCodes.Status415UnsupportedMediaType => "The request content type is not supported.",
            _ => "The request failed."
        };
}
