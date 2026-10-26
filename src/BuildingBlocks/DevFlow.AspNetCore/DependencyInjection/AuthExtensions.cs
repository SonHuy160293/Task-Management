using System.Text;
using DevFlow.AspNetCore.Options;
using DevFlow.AspNetCore.Security;
using DevFlow.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace DevFlow.AspNetCore.DependencyInjection;

public static class AuthExtensions
{
    public static IServiceCollection AddCurrentUserProvider(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserProvider, HttpContextCurrentUserProvider>();

        return services;
    }

    public static IServiceCollection AddConfiguredAuthentication(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(option => !string.IsNullOrWhiteSpace(option.SecretKey), "JWT secret key is required.")
            .Validate(option => option.SecretKey.Length >= 32, "JWT secret key must be at least 32 characters.")
            .Validate(option => !string.IsNullOrWhiteSpace(option.Issuer), "JWT issuer is required.")
            .Validate(option => !string.IsNullOrWhiteSpace(option.Audience), "JWT audience is required.")
            .Validate(option => option.AccessTokenExpirationMinutes > 0, "JWT access token expiration must be greater than zero.")
            .ValidateOnStart();

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT options are not properly configured.");

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = !environment.IsDevelopment(),
                    ValidateAudience = !environment.IsDevelopment(),
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception is SecurityTokenExpiredException)
                        {
                            context.Response.Headers["IS-TOKEN-EXPIRED"] = "True";
                        }

                        return Task.CompletedTask;
                    }
                };

                options.EventsType = typeof(CustomJwtBearerEvents);
            });

        services.AddScoped<CustomJwtBearerEvents>();

        return services;
    }

    public static IServiceCollection AddConfiguredAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization();
        services
            .AddOptions<IntegrationApiOptions>()
            .BindConfiguration(IntegrationApiOptions.SectionName)
            .Validate(option => !string.IsNullOrWhiteSpace(option.HeaderName), "Integration API header name is required.")
            .Validate(option => option.ApiKeys.Length > 0, "At least one integration API key is required.")
            .Validate(option => option.ApiKeys.All(key => !string.IsNullOrWhiteSpace(key) && key.Length <= 4096), "Integration API keys must be non-empty and no longer than 4096 characters.")
            .ValidateOnStart();

        services.AddScoped<IntegrationApiKeyFilter>();

        return services;
    }
}
