using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace DevFlow.Gateway;

// Parse into an isolated snapshot: malformed JSON must not empty live routing configuration.
public sealed class GatewayConfigProvider : IProxyConfigProvider, IDisposable
{
    private readonly IConfiguration _overrides;
    private readonly IConfigValidator _validator;
    private readonly ILogger<GatewayConfigProvider> _logger;
    private readonly PhysicalFileProvider _files;
    private readonly string _fileName;
    private readonly IDisposable _watch;
    private readonly object _reloadLock = new();
    private Snapshot _current;
    private bool _disposed;

    public GatewayConfigProvider(IConfiguration configuration, IHostEnvironment environment, IConfigValidator validator, ILogger<GatewayConfigProvider> logger)
    {
        _overrides = configuration;
        _validator = validator;
        _logger = logger;
        string path = Path.GetFullPath(configuration["Gateway:RoutesFile"] ?? "proxy.json", environment.ContentRootPath);
        _fileName = Path.GetFileName(path);
        _files = new PhysicalFileProvider(Path.GetDirectoryName(path)!);
        try
        {
            _current = Load();
            _watch = ChangeToken.OnChange(() => _files.Watch(_fileName), Reload);
        }
        catch
        {
            _files.Dispose();
            throw;
        }
    }

    public IProxyConfig GetConfig() => Volatile.Read(ref _current);

    public void Dispose()
    {
        _watch.Dispose();
        lock (_reloadLock)
        {
            _disposed = true;
            _files.Dispose();
            _current.Dispose();
        }
    }

    internal static bool IsSafeToken(string value) => value.Length is > 0 and <= 100
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');

    private Snapshot Load()
    {
        using var config = (ConfigurationRoot)new ConfigurationBuilder().AddJsonFile(_files, _fileName, optional: false, reloadOnChange: false)
            .AddConfiguration(_overrides).Build();
        var routes = config.GetSection("ReverseProxy:Routes").GetChildren()
            .Select(section => (section.Get<RouteConfig>() ?? throw new InvalidOperationException("Invalid route.")) with { RouteId = section.Key }).ToArray();
        var clusters = config.GetSection("ReverseProxy:Clusters").GetChildren()
            .Select(section => (section.Get<ClusterConfig>() ?? throw new InvalidOperationException("Invalid cluster.")) with { ClusterId = section.Key }).ToArray();
        if (routes.Length == 0 || clusters.Length == 0)
        {
            throw new InvalidOperationException("At least one route and cluster are required.");
        }

        foreach (var route in routes)
        {
            if (!IsSafeToken(route.RouteId) || route.ClusterId is null || !clusters.Any(cluster => cluster.ClusterId == route.ClusterId)
                || route.Timeout is null || route.Timeout <= TimeSpan.Zero || route.Timeout > TimeSpan.FromSeconds(30)
                || route.MaxRequestBodySize is null or <= 0 or > 65536
                || route.Match?.Methods is null or { Count: 0 }
                || string.IsNullOrWhiteSpace(route.Match?.Path) || route.Match.Path.Contains('*', StringComparison.Ordinal)
                || _validator.ValidateRouteAsync(route).AsTask().GetAwaiter().GetResult().Count != 0)
            {
                throw new InvalidOperationException("Invalid gateway route configuration.");
            }
        }

        foreach (var cluster in clusters)
        {
            if (!IsSafeToken(cluster.ClusterId) || _validator.ValidateClusterAsync(cluster).AsTask().GetAwaiter().GetResult().Count != 0
                || cluster.Destinations?.Any(destination => !IsSafeToken(destination.Key)
                    || !Uri.TryCreate(destination.Value.Address, UriKind.Absolute, out var uri)
                    || uri.Scheme is not("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)) == true)
            {
                throw new InvalidOperationException("Invalid gateway cluster configuration.");
            }
        }

        return new Snapshot(routes, clusters);
    }

    private void Reload()
    {
        lock (_reloadLock)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                Snapshot replacement = Load();
                Snapshot previous = Interlocked.Exchange(ref _current, replacement);
                previous.Signal();
                previous.Dispose();
                _logger.LogInformation("Gateway configuration accepted");
            }
            catch (Exception)
            {
                // Never include parser/validator exceptions, destinations, or configuration values in logs.
                _logger.LogWarning("Gateway configuration rejected");
            }
        }
    }

    private sealed class Snapshot(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters) : IProxyConfig, IDisposable
    {
        private readonly CancellationTokenSource _changed = new();
        public IReadOnlyList<RouteConfig> Routes { get; } = routes;
        public IReadOnlyList<ClusterConfig> Clusters { get; } = clusters;
        public IChangeToken ChangeToken => new CancellationChangeToken(_changed.Token);

        public void Signal() => _changed.Cancel();
        public void Dispose() => _changed.Dispose();
    }
}
