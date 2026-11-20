using System.Net.Http.Json;
using Janus.Customers;
using Janus.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Tests;

/// <summary>
/// AC2: the same scenarios run against legacy-via-adapter and against the new module; results must be equal.
/// Run in CI on every push: a red contract test blocks a flip to New.
/// </summary>
public class ContractTests
{
    [Theory]
    [InlineData("/customers")]
    [InlineData("/customers/10042")]
    [InlineData("/customers/10077")]
    [InlineData("/customers/10105")]
    [InlineData("/customers/10213")]
    [InlineData("/customers/10388")]
    [InlineData("/customers/10450")]
    [InlineData("/customers/00010042")]
    [InlineData("/customers/99999")]
    [InlineData("/customers/not-a-number")]
    public async Task Legacy_via_adapter_and_new_module_agree_over_http(string path)
    {
        await using var legacy = await Hosts.StartLegacyAsync();
        await using var viaLegacy = await Hosts.StartFacadeAsync(legacy.Client.BaseAddress!,
            new Dictionary<string, string?> { ["Bridge:Capabilities:customers:Mode"] = "Legacy" });
        await using var viaNew = await Hosts.StartFacadeAsync(legacy.Client.BaseAddress!,
            new Dictionary<string, string?> { ["Bridge:Capabilities:customers:Mode"] = "New" });

        var old = await viaLegacy.Client.GetAsync(new Uri(path, UriKind.Relative));
        var @new = await viaNew.Client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal("legacy", string.Join(",", old.Headers.GetValues("X-Served-By")));
        Assert.Equal("new", string.Join(",", @new.Headers.GetValues("X-Served-By")));
        Assert.Equal(old.StatusCode, @new.StatusCode);
        if (old.IsSuccessStatusCode)
        {
            Assert.Equal(await old.Content.ReadAsStringAsync(), await @new.Content.ReadAsStringAsync());
        }
        else
        {
            var oldCode = (await old.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["code"].ToString();
            var newCode = (await @new.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["code"].ToString();
            Assert.Equal(oldCode, newCode);
        }
    }

    [Fact]
    public async Task Every_legacy_record_translates_to_the_new_modules_record()
    {
        await using var legacy = await Hosts.StartLegacyAsync();
        var services = new ServiceCollection().AddCustomersModule(seedDemoData: true).BuildServiceProvider();
        var translator = services.GetRequiredService<ILegacyCustomerTranslator>();
        var directory = services.GetRequiredService<ICustomerDirectory>();

        var raw = await legacy.Client.GetStringAsync(new Uri("/TERM/CUST", UriKind.Relative));
        var translated = translator.TranslateMany(raw);
        var modern = await directory.ListAsync();

        Assert.True(translated.IsSuccess);
        Assert.Equal(modern.Count, translated.Value.Count);
        foreach (var record in translated.Value)
        {
            Assert.Equal((await directory.FindAsync(record.CustomerNumber)).Value, record);
        }
    }
}
