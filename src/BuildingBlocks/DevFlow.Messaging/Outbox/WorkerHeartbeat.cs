using System.Globalization;

namespace DevFlow.Messaging.Outbox;

public sealed class WorkerHeartbeat
{
    private readonly string _path;

    public WorkerHeartbeat(string? path = null)
    {
        _path = path
            ?? Environment.GetEnvironmentVariable("DEVFLOW_WORKER_HEARTBEAT_FILE")
            ?? Path.Combine(Path.GetTempPath(), "devflow-worker.heartbeat");
    }

    public async Task CompleteScanAsync(CancellationToken cancellationToken)
    {
        string temporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            await File.WriteAllTextAsync(temporaryPath, timestamp, cancellationToken);
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
