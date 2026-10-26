using DevFlow.AspNetCore.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevFlow.AspNetCore.DependencyInjection;

public static class CorsExtensions
{
    public static IServiceCollection AddConfiguredCors(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName))
            .Validate(option => option.AllowedOrigins.Length > 0, "CORS must define at least one allowed origin.")
            .Validate(option => option.AllowedMethods.Length > 0, "CORS must define at least one allowed method.")
            .Validate(option => option.AllowedHeaders.Length > 0, "CORS must define at least one allowed header.")
            .Validate(option => option.AllowedOrigins.All(origin => origin != "*"), "CORS must not use wildcard origins.")
            .Validate(option => option.PreflightMaxAgeMinutes >= 0, "CORS preflight max age must be greater than or equal to zero.")
            .ValidateOnStart();

        var corsOptions = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()
            ?? throw new InvalidOperationException("CORS options are not properly configured.");

        services.AddCors(options =>
        {
            options.AddPolicy(CorsOptions.PolicyName, policy =>
            {
                policy
                    .WithOrigins(corsOptions.AllowedOrigins)
                    .WithMethods(corsOptions.AllowedMethods)
                    .WithHeaders(corsOptions.AllowedHeaders)
                    .SetPreflightMaxAge(TimeSpan.FromMinutes(corsOptions.PreflightMaxAgeMinutes));

                if (corsOptions.ExposedHeaders.Length > 0)
                {
                    policy.WithExposedHeaders(corsOptions.ExposedHeaders);
                }

                if (corsOptions.AllowCredentials)
                {
                    policy.AllowCredentials();
                }
                else
                {
                    policy.DisallowCredentials();
                }
            });
        });

        return services;
    }
}
