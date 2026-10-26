using System.Diagnostics;
using DevFlow.AspNetCore.HttpClient.Abstractions;
using DevFlow.AspNetCore.HttpClient.Observability;
using Microsoft.Extensions.Logging;

namespace DevFlow.AspNetCore.HttpClient.Handlers;

internal sealed class IntegrationTelemetryHandler(
    string dependencyName,
    ICorrelationIdAccessor correlationIdAccessor,
    ILogger<IntegrationTelemetryHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        long started = Stopwatch.GetTimestamp();
        string failureKind = "None";
        int statusCode = 0;

        try
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            statusCode = (int)response.StatusCode;
            failureKind = response.IsSuccessStatusCode ? "None" : "HttpFailure";
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            failureKind = "Cancelled";
            throw;
        }
        catch (Polly.Timeout.TimeoutRejectedException)
        {
            failureKind = "Timeout";
            throw;
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException)
        {
            failureKind = "CircuitOpen";
            throw;
        }
        catch (HttpRequestException)
        {
            failureKind = "NetworkFailure";
            throw;
        }
        finally
        {
            if (failureKind != "None" || statusCode >= 400)
            {
                logger.LogWarning(
                    HttpClientTelemetry.CompletionTemplate,
                    dependencyName,
                    NormalizeMethod(request.Method),
                    statusCode,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    failureKind,
                    correlationIdAccessor.GetCorrelationId()?.ToString("D") ?? "None");
            }
        }
    }

    private static string NormalizeMethod(HttpMethod method)
        => method.Method is "GET" or "HEAD" or "POST" or "PUT" or "PATCH" or "DELETE" or "OPTIONS"
            ? method.Method
            : "OTHER";
}
