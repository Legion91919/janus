namespace Janus.Facade.Legacy;

public enum CircuitState
{
    Closed,
    Open,
    HalfOpen,
}

/// <summary>The old terminal could not answer: <see cref="Reason"/> is "circuit-open", "timeout" or "error".</summary>
public sealed class LegacyUnavailableException : Exception
{
    public LegacyUnavailableException() : this("error") { }

    public LegacyUnavailableException(string reason) : base($"The legacy system is unavailable ({reason}).") => Reason = reason;

    public LegacyUnavailableException(string message, Exception innerException) : base(message, innerException) => Reason = "error";

    public string Reason { get; }
}

/// <summary>Full jitter: a random delay between 0 and min(max, base * 2^attempt).</summary>
internal static class Backoff
{
    public static TimeSpan Delay(int attempt, TimeSpan baseDelay, TimeSpan max, double random01)
    {
        var ceiling = Math.Min(max.TotalMilliseconds, baseDelay.TotalMilliseconds * Math.Pow(2, Math.Min(attempt, 30)));
        return TimeSpan.FromMilliseconds(Math.Round(ceiling * Math.Clamp(random01, 0, 1)));
    }
}

/// <summary>
/// Consecutive-failure circuit breaker. Closed: calls flow. Open: calls fail fast for BreakerOpenSeconds.
/// HalfOpen: one trial call; success closes, failure re-opens. Thresholds come from options on every call, so they reload.
/// </summary>
internal sealed class CircuitBreaker(TimeProvider time)
{
    private readonly object _gate = new();
    private CircuitState _state = CircuitState.Closed;
    private int _failures;
    private DateTimeOffset _openedAt;
    private bool _trialInFlight;

    public CircuitState State(LegacyOptions options)
    {
        lock (_gate) { return Current(options); }
    }

    public bool TryAcquire(LegacyOptions options)
    {
        lock (_gate)
        {
            switch (Current(options))
            {
                case CircuitState.Closed:
                    return true;
                case CircuitState.HalfOpen when !_trialInFlight:
                    _state = CircuitState.HalfOpen;
                    _trialInFlight = true;
                    return true;
                default:
                    return false;
            }
        }
    }

    public void RecordSuccess()
    {
        lock (_gate)
        {
            _state = CircuitState.Closed;
            _failures = 0;
            _trialInFlight = false;
        }
    }

    public void RecordFailure(LegacyOptions options)
    {
        lock (_gate)
        {
            _failures++;
            if (_state == CircuitState.HalfOpen || _failures >= options.BreakerFailureThreshold)
            {
                _state = CircuitState.Open;
                _openedAt = time.GetUtcNow();
            }

            _trialInFlight = false;
        }
    }

    /// <summary>A trial call was cancelled by the caller: let the next request try instead.</summary>
    public void ReleaseTrial()
    {
        lock (_gate) { _trialInFlight = false; }
    }

    private CircuitState Current(LegacyOptions options) =>
        _state == CircuitState.Open && time.GetUtcNow() - _openedAt >= TimeSpan.FromSeconds(options.BreakerOpenSeconds)
            ? CircuitState.HalfOpen
            : _state;
}

/// <summary>Runs one logical call: breaker check, per-attempt timeout, retry transient failures with jittered back-off.</summary>
internal sealed class ResilientCaller(CircuitBreaker breaker, TimeProvider time, Func<double> random)
{
    public CircuitBreaker Breaker => breaker;

    public async Task<T> ExecuteAsync<T>(LegacyOptions options, Func<CancellationToken, Task<T>> action, Func<T, bool> isTransient, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            if (!breaker.TryAcquire(options)) throw new LegacyUnavailableException("circuit-open");

            string failure;
            using (var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(options.TimeoutMs), time))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token))
            {
                try
                {
                    var result = await action(linked.Token).ConfigureAwait(false);
                    if (!isTransient(result))
                    {
                        breaker.RecordSuccess();
                        return result;
                    }

                    failure = "error";
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    failure = "timeout";
                }
                catch (HttpRequestException)
                {
                    failure = "error";
                }
                catch (OperationCanceledException)
                {
                    breaker.ReleaseTrial();
                    throw;
                }
            }

            breaker.RecordFailure(options);
            if (attempt >= options.MaxRetries) throw new LegacyUnavailableException(failure);
            var delay = Backoff.Delay(attempt, TimeSpan.FromMilliseconds(options.BaseDelayMs), TimeSpan.FromMilliseconds(options.MaxDelayMs), random());
            await Task.Delay(delay, time, ct).ConfigureAwait(false);
        }
    }
}
