using System.Net;
using System.Text;
using DevFlow.Identity.API.Configuration;
using DevFlow.Identity.API.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DevFlow.Identity.Tests.Security;

public sealed class IdentityHostConfigurationTests
{
    [Fact]
    public void SecretFilesOverrideJsonAndEnvironmentWithoutLosingOtherSettings()
    {
        string directory = Path.Combine(Path.GetTempPath(), "devflow-config-" + Guid.NewGuid().ToString("N"));
        string environmentPrefix = "DEVFLOW_CONFIG_TEST_" + Guid.NewGuid().ToString("N") + "_";
        string[] keys = ["Jwt__SecretKey", "IntegrationApi__ApiKeys__0", "Identity__Security__RegistrationRateLimit__KeySecretBase64"];
        Directory.CreateDirectory(directory);
        try
        {
            foreach (string key in keys)
            {
                Environment.SetEnvironmentVariable(environmentPrefix + key, "environment-value");
                File.WriteAllText(Path.Combine(directory, key), "file-value");
            }

            using var json = new MemoryStream(Encoding.UTF8.GetBytes("""{"Jwt":{"SecretKey":"json-value","Issuer":"issuer-from-json"}}"""));
            using var configuration = new ConfigurationManager();
            configuration.AddJsonStream(json);
            configuration.AddEnvironmentVariables(environmentPrefix);
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["DEVFLOW_CONFIG_SECRETS_DIRECTORY"] = directory });
            configuration.AddIdentitySecretFiles();

            foreach (string key in keys)
            {
                Assert.Equal("file-value", configuration[key.Replace("__", ":", StringComparison.Ordinal)]);
                Assert.Equal("environment-value", Environment.GetEnvironmentVariable(environmentPrefix + key));
            }

            Assert.Equal("issuer-from-json", configuration["Jwt:Issuer"]);
        }
        finally
        {
            foreach (string key in keys)
            {
                Environment.SetEnvironmentVariable(environmentPrefix + key, null);
            }

            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void MissingOptionalSecretDirectoryPreservesConfiguredValues()
    {
        string directory = Path.Combine(Path.GetTempPath(), "devflow-no-config-" + Guid.NewGuid().ToString("N"));
        using var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DEVFLOW_CONFIG_SECRETS_DIRECTORY"] = directory,
            ["Jwt:SecretKey"] = "existing-value",
        });
        configuration.AddIdentitySecretFiles();
        Assert.Equal("existing-value", configuration["Jwt:SecretKey"]);
        Assert.False(Directory.Exists(directory));
    }

    [Theory]
    [InlineData("Production", HttpStatusCode.TemporaryRedirect)]
    [InlineData("Development", HttpStatusCode.Accepted)]
    public async Task HealthProbesUseHttpWhileProductionBusinessRequestsRequireHttps(string environment, HttpStatusCode expected)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Services.AddIdentityHttpsRedirection(builder.Configuration);
        await using var app = builder.Build();
        app.UseIdentityHttpsRedirection();
        app.MapGet("/health/live", () => Results.Ok());
        app.MapGet("/health/ready", () => Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
        app.MapPost("/api/v1/auth/register", () => Results.Accepted());
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var live = await client.GetAsync("/health/live");
        using var ready = await client.GetAsync("/health/ready");
        using var registration = await client.PostAsync("/api/v1/auth/register", null);

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Null(ready.Headers.Location);
        Assert.Equal(expected, registration.StatusCode);
        if (environment == Environments.Production)
        {
            Assert.Equal("https", registration.Headers.Location!.Scheme);
        }
    }
}
