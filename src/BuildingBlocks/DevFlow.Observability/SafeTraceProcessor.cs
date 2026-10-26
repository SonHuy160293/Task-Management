using System.Diagnostics;
using OpenTelemetry;

namespace DevFlow.Observability;

public sealed class SafeTraceProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity data)
    {
        foreach (var tag in data.TagObjects.ToArray())
        {
            if (tag.Key is not("http.request.method" or "http.response.status_code" or "http.route"
                or "http.method" or "http.status_code" or "db.system" or "db.system.name"
                or "cqrs.request_type" or "cqrs.outcome" or "error.type"))
            {
                data.SetTag(tag.Key, null);
            }
        }

        // SQL span names can contain SQL text or procedure names; HTTP names can contain raw URLs.
        if (data.Source.Name != "DevFlow.Cqrs")
        {
            data.DisplayName = data.Kind == ActivityKind.Server ? "HTTP request" : "Dependency request";
        }

        data.SetStatus(data.Status);
        data.TraceStateString = null;
    }
}
