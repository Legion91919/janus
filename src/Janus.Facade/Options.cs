using Janus.Facade.Legacy;

namespace Janus.Facade;

/// <summary>Where a capability's traffic goes. Shadow serves legacy and compares with new in the background.</summary>
public enum RoutingMode
{
    Legacy,
    New,
    Shadow,
}

/// <summary>Bound from the <c>Bridge</c> configuration section and re-read on every request, so a config reload flips traffic.</summary>
public sealed class BridgeOptions
{
    public const string Section = "Bridge";

    public Dictionary<string, CapabilityOptions> Capabilities { get; } = new(StringComparer.OrdinalIgnoreCase);

    public LegacyOptions Legacy { get; set; } = new();

    /// <summary>Allows <c>PUT/DELETE /bridge/flags/{capability}</c>. On in Development; production flips by configuration.</summary>
    public bool AllowRuntimeFlagChanges { get; set; }

    public RoutingMode ModeFor(string capability) =>
        Capabilities.TryGetValue(capability, out var c) ? c.Mode : RoutingMode.Legacy;
}

public sealed class CapabilityOptions
{
    public RoutingMode Mode { get; set; } = RoutingMode.Legacy;
}
