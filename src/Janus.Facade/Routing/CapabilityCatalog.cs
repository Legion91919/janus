namespace Janus.Facade.Routing;

/// <summary>A business capability the facade routes: its public path and where it lives on the old terminal.</summary>
internal sealed record Capability(string Name, string PublicPrefix, string LegacyPrefix);

internal readonly record struct CapabilityMatch(Capability Capability, string Rest);

internal static class CapabilityCatalog
{
    public static readonly IReadOnlyList<Capability> All =
    [
        new("customers", "/customers", "/TERM/CUST"),
        new("schedules", "/schedules", "/TERM/SCHED"),
        new("arrears", "/arrears", "/TERM/ARREARS"),
    ];

    public static Capability? Find(string name) =>
        All.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Matches on whole path segments: "/customers/1" matches, "/customersX" does not.</summary>
    public static CapabilityMatch? Match(string path)
    {
        foreach (var capability in All)
        {
            if (!path.StartsWith(capability.PublicPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            var rest = path[capability.PublicPrefix.Length..];
            if (rest.Length == 0 || rest[0] == '/') return new CapabilityMatch(capability, rest.TrimEnd('/'));
        }

        return null;
    }
}
