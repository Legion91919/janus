namespace Janus.Facade;

/// <summary>
/// The last configuration provider: holds operator overrides set through <c>PUT /bridge/flags/{capability}</c> and
/// raises a reload so <c>IOptionsMonitor&lt;BridgeOptions&gt;</c> sees them on the next request. File edits to
/// appsettings.json reload the same way; an override wins until it is cleared or the process restarts.
/// </summary>
internal sealed class RuntimeFlagsProvider : ConfigurationProvider, IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) => this;

    public void SetMode(string capability, RoutingMode mode)
    {
        Data[Key(capability)] = mode.ToString();
        OnReload();
    }

    public void Clear(string capability)
    {
        Data.Remove(Key(capability));
        OnReload();
    }

    private static string Key(string capability) => $"{BridgeOptions.Section}:Capabilities:{capability}:Mode";
}
