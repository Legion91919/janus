using Agentic.Blocks.Core;
using Agentic.Blocks.Web;
using Janus.Customers;
using Janus.Facade.Legacy;
using Janus.Facade.Routing;
using Janus.Facade.Shadow;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Janus.Facade;

/// <summary>Composition root for the facade (port 5206). Program.cs and the tests both call these two methods.</summary>
public static class FacadeApp
{
    public static WebApplicationBuilder AddBridgeFacade(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var flags = new RuntimeFlagsProvider();
        ((IConfigurationBuilder)builder.Configuration).Add(flags);

        var services = builder.Services;
        services.AddBlocksWeb();
        services.Configure<BridgeOptions>(builder.Configuration.GetSection(BridgeOptions.Section));
        services.AddSingleton(flags);
        services.AddCustomersModule(seedDemoData: builder.Environment.IsDevelopment());
        services.AddScoped<INewCapability, CustomersCapability>();

        services.AddSingleton<CircuitBreaker>();
        services.AddSingleton(sp => new ResilientCaller(sp.GetRequiredService<CircuitBreaker>(), sp.GetRequiredService<TimeProvider>(), Random.Shared.NextDouble));
        services.AddHttpClient<LegacyClient>(c => c.Timeout = Timeout.InfiniteTimeSpan);

        services.AddSingleton<ShadowQueue>();
        services.AddSingleton<ShadowReport>();
        services.AddHostedService<ShadowComparer>();

        services.AddHealthChecks().AddCheck<LegacyBreakerHealthCheck>("legacy-breaker", tags: [BlocksHealthTags.Ready]);
        return builder;
    }

    public static WebApplication UseBridgeFacade(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseBlocksWeb();
        app.UseMiddleware<BridgeMiddleware>();

        var bridge = app.MapGroup("/bridge");
        bridge.MapGet("/status", Status);
        bridge.MapPut("/flags/{capability}", SetFlag);
        bridge.MapDelete("/flags/{capability}", ClearFlag);
        return app;
    }

    private static IResult Status(IOptionsMonitor<BridgeOptions> options, ShadowReport report, CircuitBreaker breaker, IServiceProvider services)
    {
        var current = options.CurrentValue;
        var migrated = services.GetServices<INewCapability>().Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Results.Ok(new
        {
            breaker = breaker.State(current.Legacy).ToString(),
            capabilities = CapabilityCatalog.All.Select(c => new
            {
                name = c.Name,
                mode = (migrated.Contains(c.Name) ? current.ModeFor(c.Name) : RoutingMode.Legacy).ToString(),
                migrated = migrated.Contains(c.Name),
                shadow = report.Snapshot(c.Name),
            }),
        });
    }

    private static IResult SetFlag(string capability, FlagChange change, IOptionsMonitor<BridgeOptions> options, RuntimeFlagsProvider flags, IServiceProvider services)
    {
        if (!options.CurrentValue.AllowRuntimeFlagChanges) return Results.NotFound();
        if (CapabilityCatalog.Find(capability) is not { } cap) return Error.NotFound("bridge.unknown_capability", "No such capability.").ToProblem();
        if (!Enum.TryParse<RoutingMode>(change.Mode, ignoreCase: true, out var mode) || !Enum.IsDefined(mode))
        {
            return Error.Validation("bridge.invalid_mode", "mode", "Mode is Legacy, Shadow or New.").ToProblem();
        }

        var migrated = services.GetServices<INewCapability>().Any(c => c.Name == cap.Name);
        if (!migrated && mode != RoutingMode.Legacy)
        {
            return Error.Validation("bridge.not_migrated", "mode", $"{cap.Name} has no new implementation yet; it can only be Legacy.").ToProblem();
        }

        flags.SetMode(cap.Name, mode);
        return Results.NoContent();
    }

    private static IResult ClearFlag(string capability, IOptionsMonitor<BridgeOptions> options, RuntimeFlagsProvider flags)
    {
        if (!options.CurrentValue.AllowRuntimeFlagChanges) return Results.NotFound();
        if (CapabilityCatalog.Find(capability) is not { } cap) return Error.NotFound("bridge.unknown_capability", "No such capability.").ToProblem();
        flags.Clear(cap.Name);
        return Results.NoContent();
    }
}

internal sealed record FlagChange(string? Mode);

/// <summary>Ready stays 200 when the old terminal's breaker is open (Degraded): capabilities on New still work.</summary>
internal sealed class LegacyBreakerHealthCheck(CircuitBreaker breaker, IOptionsMonitor<BridgeOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(breaker.State(options.CurrentValue.Legacy) == CircuitState.Closed
            ? HealthCheckResult.Healthy("Legacy breaker closed")
            : HealthCheckResult.Degraded("Legacy breaker open or half-open"));
}
