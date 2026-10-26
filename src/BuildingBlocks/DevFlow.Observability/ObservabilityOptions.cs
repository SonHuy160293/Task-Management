namespace DevFlow.Observability;

public sealed class ObservabilityOptions
{
    public bool Enabled { get; set; } = true;

    public string? ServiceName { get; set; }

    public string? ServiceVersion { get; set; }

    public string Endpoint { get; set; } = "http://localhost:4317";

    public double TraceSamplingRatio { get; set; } = 1;

    public int ExportTimeoutMilliseconds { get; set; } = 3000;

    public int MetricExportIntervalMilliseconds { get; set; } = 10000;
}
