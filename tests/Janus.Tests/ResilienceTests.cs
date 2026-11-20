using Agentic.Blocks.Testing;
using Janus.Facade.Legacy;
using Xunit;

namespace Janus.Tests;

/// <summary>Hand-rolled timeout, retry with jitter and circuit breaker around the old terminal.</summary>
public class ResilienceTests
{
    private static LegacyOptions Options(int retries = 2, int threshold = 5, int timeoutMs = 2000) => new()
    {
        MaxRetries = retries, BaseDelayMs = 0, MaxDelayMs = 0, BreakerFailureThreshold = threshold, BreakerOpenSeconds = 30, TimeoutMs = timeoutMs,
    };

    private static async Task<Exception?> Catch(Func<Task> act)
    {
        try { await act(); return null; }
        catch (Exception ex) { return ex; }
    }

    [Theory]
    [InlineData(0, 1.0, 100)]
    [InlineData(1, 1.0, 200)]
    [InlineData(3, 1.0, 800)]
    [InlineData(6, 1.0, 1000)]
    [InlineData(3, 0.5, 400)]
    [InlineData(3, 0.0, 0)]
    public void Backoff_is_exponential_capped_and_fully_jittered(int attempt, double random, int expectedMs)
    {
        var delay = Backoff.Delay(attempt, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1), random);

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), delay);
    }

    [Fact]
    public void Breaker_opens_after_threshold_then_half_opens_after_the_wait()
    {
        var clock = new ManualTimeProvider();
        var breaker = new CircuitBreaker(clock);
        var options = Options(threshold: 3);

        for (var i = 0; i < 3; i++) { Assert.True(breaker.TryAcquire(options)); breaker.RecordFailure(options); }
        Assert.Equal(CircuitState.Open, breaker.State(options));
        Assert.False(breaker.TryAcquire(options));

        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(CircuitState.HalfOpen, breaker.State(options));
        Assert.True(breaker.TryAcquire(options));
        Assert.False(breaker.TryAcquire(options));   // one trial call at a time
        breaker.RecordSuccess();
        Assert.Equal(CircuitState.Closed, breaker.State(options));
    }

    [Fact]
    public void Failed_trial_call_reopens_the_breaker()
    {
        var clock = new ManualTimeProvider();
        var breaker = new CircuitBreaker(clock);
        var options = Options(threshold: 1);
        breaker.TryAcquire(options);
        breaker.RecordFailure(options);
        clock.Advance(TimeSpan.FromSeconds(30));

        Assert.True(breaker.TryAcquire(options));
        breaker.RecordFailure(options);

        Assert.Equal(CircuitState.Open, breaker.State(options));
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.False(breaker.TryAcquire(options));
    }

    [Fact]
    public void Success_resets_the_consecutive_failure_count()
    {
        var breaker = new CircuitBreaker(new ManualTimeProvider());
        var options = Options(threshold: 2);

        breaker.RecordFailure(options);
        breaker.RecordSuccess();
        breaker.RecordFailure(options);

        Assert.Equal(CircuitState.Closed, breaker.State(options));
    }

    [Fact]
    public async Task Transient_failures_are_retried_until_success()
    {
        var caller = new ResilientCaller(new CircuitBreaker(TimeProvider.System), TimeProvider.System, () => 0.5);
        var calls = 0;

        var result = await caller.ExecuteAsync(Options(retries: 2), _ =>
        {
            calls++;
            return Task.FromResult(calls < 3 ? 503 : 200);
        }, status => status >= 500, CancellationToken.None);

        Assert.Equal(200, result);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task Non_transient_results_are_not_retried()
    {
        var caller = new ResilientCaller(new CircuitBreaker(TimeProvider.System), TimeProvider.System, () => 0.5);
        var calls = 0;

        var result = await caller.ExecuteAsync(Options(), _ => { calls++; return Task.FromResult(404); }, s => s >= 500, CancellationToken.None);

        Assert.Equal(404, result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Exhausted_retries_throw_legacy_unavailable()
    {
        var caller = new ResilientCaller(new CircuitBreaker(TimeProvider.System), TimeProvider.System, () => 0.5);
        var calls = 0;

        var ex = await Catch(() => caller.ExecuteAsync<int>(Options(retries: 2), _ =>
        {
            calls++;
            throw new HttpRequestException("connection refused");
        }, _ => false, CancellationToken.None));

        Assert.Equal(3, calls);
        Assert.Equal("error", Assert.Throws<LegacyUnavailableException>(() => throw ex!).Reason);
    }

    [Fact]
    public async Task Slow_calls_time_out_and_count_as_failures()
    {
        var caller = new ResilientCaller(new CircuitBreaker(TimeProvider.System), TimeProvider.System, () => 0.5);

        var ex = await Catch(() => caller.ExecuteAsync(Options(retries: 1, timeoutMs: 50), async ct =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return 200;
        }, _ => false, CancellationToken.None));

        Assert.Equal("timeout", Assert.Throws<LegacyUnavailableException>(() => throw ex!).Reason);
    }

    [Fact]
    public async Task Open_breaker_fails_fast_without_calling_legacy()
    {
        var breaker = new CircuitBreaker(new ManualTimeProvider());
        var options = Options(retries: 0, threshold: 1);
        var caller = new ResilientCaller(breaker, TimeProvider.System, () => 0.5);
        breaker.TryAcquire(options);
        breaker.RecordFailure(options);
        var calls = 0;

        var ex = await Catch(() => caller.ExecuteAsync(options, _ => { calls++; return Task.FromResult(200); }, _ => false, CancellationToken.None));

        Assert.Equal(0, calls);
        Assert.Equal("circuit-open", Assert.Throws<LegacyUnavailableException>(() => throw ex!).Reason);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_swallowed_as_a_timeout()
    {
        var caller = new ResilientCaller(new CircuitBreaker(TimeProvider.System), TimeProvider.System, () => 0.5);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var ex = await Catch(() => caller.ExecuteAsync(Options(), ct => Task.FromCanceled<int>(ct), _ => false, cts.Token));

        Assert.True(ex is OperationCanceledException);
    }
}
