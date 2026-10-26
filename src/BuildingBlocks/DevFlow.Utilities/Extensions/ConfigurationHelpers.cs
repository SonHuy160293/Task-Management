using Microsoft.Extensions.Configuration;
namespace DevFlow.AspNetCore.Configuration;

public static class ConfigurationHelpers
{
    public static int ReadInt(
        this IConfiguration configuration,
        string key,
        int defaultValue)
        => int.TryParse(configuration[key], out var value)
            ? value
            : defaultValue;
}
