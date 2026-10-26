using System.Diagnostics;
using DevFlow.SharedKernel.Abstractions.Message;
using DevFlow.SharedKernel.Abstractions.Shared;
using Microsoft.Extensions.Logging;

namespace DevFlow.SharedKernel.Behaviors;

public sealed class TracingPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public const string ActivitySourceName = "DevFlow.Cqrs";

    private static readonly ActivitySource _activitySource = new(ActivitySourceName);

    private readonly ILogger<TRequest> _logger;

    public TracingPipelineBehavior(ILogger<TRequest> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity(typeof(TRequest).Name);
        activity?.SetTag("cqrs.request_type", typeof(TRequest).Name);
        var stopwatch = Stopwatch.StartNew();
        string outcome = "failure";
        try
        {
            var response = await next();
            outcome = response is Result { IsFailure: true } ? "failure" : "success";
            activity?.SetTag("cqrs.outcome", outcome);
            if (outcome == "failure")
            {
                activity?.SetStatus(ActivityStatusCode.Error);
            }

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = "cancelled";
            activity?.SetTag("cqrs.outcome", outcome);
            throw;
        }
        catch (Exception)
        {
            activity?.SetTag("cqrs.outcome", "failure");
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            CqrsTelemetry.Record(typeof(TRequest).Name, outcome, stopwatch.Elapsed);
            _logger.LogInformation("Request {RequestName} completed in {ElapsedMilliseconds} ms", typeof(TRequest).Name, stopwatch.ElapsedMilliseconds);
        }
    }
}
