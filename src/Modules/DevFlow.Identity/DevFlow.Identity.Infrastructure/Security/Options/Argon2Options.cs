namespace DevFlow.Identity.Infrastructure.Security.Options;

public sealed class Argon2Options
{
    public int MemoryKiB { get; set; } = 65536;
    public int Iterations { get; set; } = 3;
    public int Parallelism { get; set; } = 1;
    public int MaxConcurrency { get; set; } = 2;
    public bool IsValid() => MemoryKiB is >= 19456 and <= 131072 && Iterations is >= 2 and <= 6
        && Parallelism is >= 1 and <= 4 && MaxConcurrency is >= 1 and <= 16;
}
