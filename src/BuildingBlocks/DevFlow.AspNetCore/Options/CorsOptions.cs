namespace DevFlow.AspNetCore.Options;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";
    public const string PolicyName = "ConfiguredCors";

    public string[] AllowedOrigins { get; set; } = [];

    public string[] AllowedMethods { get; set; } = [];

    public string[] AllowedHeaders { get; set; } = [];

    public string[] ExposedHeaders { get; set; } = [];

    public bool AllowCredentials { get; set; }

    public int PreflightMaxAgeMinutes { get; set; } = 10;
}
