using System.Collections.Concurrent;
using Agentic.Blocks.Testing;
using Janus.Facade;
using Janus.LegacyTerminal;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Janus.TestSupport;

/// <summary>Starts the fake old terminal and the facade on real Kestrel ports for acceptance and contract tests.</summary>
public static class Hosts
{
    public static Task<KestrelTestHost> StartLegacyAsync() =>
        KestrelTestHost.StartAsync(b => b.Services.AddFakeTerminal(), app => app.MapFakeTerminal());

    public static Task<KestrelTestHost> StartFacadeAsync(
        Uri legacy,
        IDictionary<string, string?>? settings = null,
        Action<IServiceCollection>? overrides = null,
        LogCapture? logs = null)
    {
        var config = new Dictionary<string, string?>
        {
            ["Bridge:Legacy:BaseAddress"] = legacy.ToString(),
            ["Bridge:Legacy:BaseDelayMs"] = "0",
            ["Bridge:AllowRuntimeFlagChanges"] = "true",
        };
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>()) config[key] = value;

        return KestrelTestHost.StartAsync(
            b =>
            {
                b.Configuration.AddInMemoryCollection(config);
                if (logs is not null)
                {
                    b.Logging.AddProvider(logs);
                    b.Logging.SetMinimumLevel(LogLevel.Information);
                }

                b.AddBridgeFacade();
                overrides?.Invoke(b.Services);
            },
            app => app.UseBridgeFacade());
    }

    /// <summary>Polls until <paramref name="done"/> holds or the timeout passes (for background shadow work).</summary>
    public static async Task<T> EventuallyAsync<T>(Func<Task<T>> probe, Func<T, bool> done, int timeoutMs = 3000)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(done);
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var value = await probe().ConfigureAwait(false);
        while (!done(value) && DateTime.UtcNow < until)
        {
            await Task.Delay(25).ConfigureAwait(false);
            value = await probe().ConfigureAwait(false);
        }

        return value;
    }
}

/// <summary>Captures every log message (formatted text plus structured values) so tests can prove what is not logged.</summary>
public sealed class LogCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _messages = new();

    public IReadOnlyCollection<string> Messages => _messages.ToArray();

    public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, _messages);

    public void Dispose() { }

    private sealed class CaptureLogger(string category, ConcurrentQueue<string> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? string.Join(";", pairs.Select(p => $"{p.Key}={p.Value}"))
                : string.Empty;
            sink.Enqueue($"{category}|{logLevel}|{formatter(state, exception)}|{values}");
        }
    }
}
