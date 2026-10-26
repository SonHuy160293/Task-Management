using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DevFlow.SharedKernel.Behaviors;

internal static class CqrsTelemetry
{
    private static readonly Meter _meter = new("DevFlow.Cqrs");
    private static readonly Counter<long> _requests = _meter.CreateCounter<long>("devflow.cqrs.requests");
    private static readonly Histogram<double> _duration = _meter.CreateHistogram<double>("devflow.cqrs.duration", "s");

    internal static void Record(string requestType, string outcome, TimeSpan elapsed)
    {
        var tags = new TagList { { "cqrs.request_type", requestType }, { "cqrs.outcome", outcome } };
        _requests.Add(1, tags);
        _duration.Record(elapsed.TotalSeconds, tags);
    }
}
