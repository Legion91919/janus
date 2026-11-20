namespace Janus.Facade.Legacy;

/// <summary>How the facade calls the old terminal: per-attempt timeout, retries with full jitter, circuit breaker.</summary>
public sealed class LegacyOptions
{
    public Uri BaseAddress { get; set; } = new("http://localhost:5207");

    public int TimeoutMs { get; set; } = 2000;

    public int MaxRetries { get; set; } = 2;

    public int BaseDelayMs { get; set; } = 100;

    public int MaxDelayMs { get; set; } = 1000;

    public int BreakerFailureThreshold { get; set; } = 5;

    public int BreakerOpenSeconds { get; set; } = 30;

    internal LegacyOptions WithoutRetries() => new()
    {
        BaseAddress = BaseAddress,
        TimeoutMs = TimeoutMs,
        MaxRetries = 0,
        BaseDelayMs = BaseDelayMs,
        MaxDelayMs = MaxDelayMs,
        BreakerFailureThreshold = BreakerFailureThreshold,
        BreakerOpenSeconds = BreakerOpenSeconds,
    };
}
