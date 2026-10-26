using System.Diagnostics;
using System.Diagnostics.Metrics;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace DevFlow.Gateway;

public sealed class GatewayRequestMiddleware(RequestDelegate next, ILogger<GatewayRequestMiddleware> logger)
{
    internal const string CompletionTemplate = "Gateway {RequestMethod} {RouteId} via {ClusterId} responded {StatusCode} in {ElapsedMilliseconds} ms; {GatewayErrorCode} {ForwarderErrorCode} {TraceId} {SpanId}";
    private static readonly Meter Meter = new("DevFlow.Gateway");
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>("devflow.gateway.requests");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("devflow.gateway.duration", "s");

    public async Task InvokeAsync(HttpContext context)
    {
        long start = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
            var error = context.Features.Get<IForwarderErrorFeature>();
            if (context.RequestAborted.IsCancellationRequested)
            {
                context.Items["GatewayErrorCode"] = "Gateway.Cancelled";
                context.Abort();
            }
            else if (context.Items["GatewayTimedOut"] is true)
            {
                await GatewayErrors.WriteAsync(context, 504, "Gateway.Timeout");
            }
            else if (error is not null || (context.Response.StatusCode == 503
                && context.Features.Get<IReverseProxyFeature>() is { ProxiedDestination: null }))
            {
                int status = context.Response.StatusCode;
                if (status is < 400 or > 599)
                {
                    status = 502;
                }

                string errorCode = status switch
                {
                    413 => "Gateway.PayloadTooLarge",
                    504 => "Gateway.Timeout",
                    503 => "Gateway.Unavailable",
                    400 or 408 => "Gateway.BadRequest",
                    _ => "Gateway.ForwardingFailed",
                };
                await GatewayErrors.WriteAsync(context, status, errorCode);
            }
            else if (context.GetEndpoint()?.Metadata.GetMetadata<RouteModel>() is null
                && context.Response.StatusCode is 404 or 405)
            {
                string errorCode = context.Response.StatusCode == 404 ? "Gateway.NotFound" : "Gateway.MethodNotAllowed";
                await GatewayErrors.WriteAsync(context, context.Response.StatusCode, errorCode);
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Items["GatewayErrorCode"] = "Gateway.Cancelled";
            context.Abort();
        }
        catch (BadHttpRequestException exception)
        {
            string errorCode = exception.StatusCode == 413 ? "Gateway.PayloadTooLarge" : "Gateway.BadRequest";
            await GatewayErrors.WriteAsync(context, exception.StatusCode, errorCode);
        }
        catch (Exception)
        {
            await GatewayErrors.WriteAsync(context, 500, "Gateway.Unhandled");
        }
        finally
        {
            var feature = context.Features.Get<IReverseProxyFeature>();
            string route = feature?.Route.Config.RouteId ?? "None";
            string cluster = feature?.Cluster.Config.ClusterId ?? "None";
            string destination = feature?.ProxiedDestination?.DestinationId ?? "None";
            string method = context.Request.Method is "GET" or "HEAD" or "POST" or "PUT" or "PATCH" or "DELETE" or "OPTIONS" ? context.Request.Method : "OTHER";
            string code = context.Items["GatewayErrorCode"] as string ?? "None";
            string forwarder = context.Features.Get<IForwarderErrorFeature>()?.Error.ToString() ?? "None";
            double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var tags = new TagList { { "http.route", route }, { "http.request.method", method }, { "http.response.status_code", context.Response.StatusCode }, { "error.type", code } };
            Requests.Add(1, tags);
            Duration.Record(elapsed / 1000, tags);
            using (Serilog.Context.LogContext.PushProperty("DestinationId", destination))
            {
                logger.LogInformation(
                    CompletionTemplate,
                    method,
                    route,
                    cluster,
                    context.Response.StatusCode,
                    elapsed,
                    code,
                    forwarder,
                    Activity.Current?.TraceId.ToString() ?? "None",
                    Activity.Current?.SpanId.ToString() ?? "None");
            }
        }
    }
}
