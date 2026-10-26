using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevFlow.AspNetCore.DependencyInjection;

public static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddTrustedForwarding(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardLimit = 1;
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            string[] proxies = configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];

            // An empty allowlist must never mean trust every peer.
            if (proxies.Length == 0)
            {
                proxies = [IPAddress.Loopback.ToString(), IPAddress.IPv6Loopback.ToString()];
            }

            foreach (string proxy in proxies)
            {
                if (!IPAddress.TryParse(proxy, out var address))
                {
                    throw new InvalidOperationException("Trusted forwarding requires valid proxy IP addresses.");
                }

                options.KnownProxies.Add(address);
            }

            foreach (string host in configuration.GetSection("ForwardedHeaders:AllowedHosts").Get<string[]>() ?? [])
            {
                options.AllowedHosts.Add(host);
            }
        });
        return services;
    }
}
