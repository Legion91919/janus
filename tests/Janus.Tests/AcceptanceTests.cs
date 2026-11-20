using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Janus.Customers;
using Janus.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Tests;

/// <summary>One test (or more) per slice-1 acceptance criterion, over real HTTP: facade on one port, fake terminal on another.</summary>
public class AcceptanceTests
{
    private static Dictionary<string, string?> Mode(string capability, string mode) =>
        new() { [$"Bridge:Capabilities:{capability}:Mode"] = mode };

    private static string Header(HttpResponseMessage r, string name) =>
        r.Headers.TryGetValues(name, out var v) ? string.Join(",", v) : "";

    // AC1: Given the routing layer, when /customers is called, then it is served by the legacy adapter with identical data.
    [Fact]
    public async Task Customers_in_legacy_mode_are_served_by_the_legacy_adapter_with_identical_data()
    {
        await using var legacy = await Hosts.StartLegacyAsync();
        await using var facade = await Hosts.StartFacadeAsync(legacy.Client.BaseAddress!, Mode("customers", "Legacy"));

        var response = await facade.Client.GetAsync(new Uri("/customers/10042", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("legacy", Header(response, "X-Served-By"));
        Assert.Equal("Legacy", Header(response, "X-Bridge-Mode"));
        var customer = await response.Content.ReadFromJsonAsync<CustomerDetails>();
        Assert.Equal(
            new CustomerDetails("10042", "Ines", "Calloway", new DateOnly(1985, 3, 12), "Active", "Retail", new DateOnly(2015, 6, 1), "B014"),
            customer);
    }

    [Fact]
    public async Task Customer_list_in_legacy_mode_has_every_seeded_customer()
    {
        await using var legacy = await Hosts.StartLegacyAsync();
        await using var facade = await Hosts.StartFacadeAsync(legacy.Client.BaseAddress!);

        var response = await facade.Client.GetAsync(new Uri("/customers", UriKind.Relative));
        var list = await response.Content.ReadFromJsonAsync<List<CustomerDetails>>();

        Assert.Equal("legacy", Header(response, "X-Served-By"));
        Assert.Equal(6, list!.Count);
    }

    [Fact]
    public async Task Legacy_not_found_quirk_becomes_a_404_problem()
    {
        await using var legacy = await Hosts.StartLegacyAsync();
        await using var facade = await Hosts.StartFacadeAsync(legacy.Client.BaseAddress!);

        var response = await facade.Client.GetAsync(new Uri("/customers/99999", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("legacy", Header(response, "X-Served-By"));
        Assert.Contains("customers.not_found", await response.Content.ReadAsStringAsync());
    }

    // AC3: Given a flag switch, then traffic moves to the new module without a deploy.
    [Fact]
    public async Task Runtime_flag_switch_moves_traffic_to_new_and_back()
    {
        await using var legacy = await Hosts.StartLegacyAsync();
        await using var facade = await Hosts.StartFacadeAsync(legacy.Client.BaseAddress!);
        var path = new Uri("/customers/10077", UriKind.Relative);

        var before = await facade.Client.GetAsync(path);
        var flip = await facade.Client.PutAsJsonAsync(new Uri("/bridge/flags/customers", UriKind.Relative), new { mode = "New" });
        var after = await facade.Client.GetAsync(path);
        var back = await facade.Client.DeleteAsync(new Uri("/bridge/flags/customers", UriKind.Relative));
        var rolledBack = await facade.Client.GetAsync(path);

        Assert.Equal("legacy", Header(before, "X-Served-By"));
        Assert.Equal(HttpStatusCode.NoContent, flip.StatusCode);
        Assert.Equal("new", Header(after, "X-Served-By"));
        Assert.Equal(await before.Content.ReadAsStringAsync(), await after.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, back.StatusCode);
        Assert.Equal("legacy", Header(rolledBack, "X-Served-By"));
    }

    [Fact]
    public async Task Configuration_reload_moves_traffic_without_a_restart()
    {
        await using var legacy = await Hosts.StartLegacyAsync();
        await using var facade = await Hosts.StartFacadeAsync(legacy.Client.BaseAddress!);
        var path = new Uri("/customers", UriKind.Relative);

        var before = await facade.Client.GetAsync(path);
        var config = (IConfigurationRoot)facade.App.Configuration;
        config["Bridge:Capabilities:customers:Mode"] = "New";
        config.Reload();
        var after = await facade.Client.GetAsync(path);

        Assert.Equal("legacy", Header(before, "X-Served-By"));
        Assert.Equal("new", Header(after, "X-Served-By"));
    }

    [Fact]
    public async Task Unmigrated_capability_cannot_be_switched_to_new()
    {
        await using var legacy = await Hosts.StartLegacyAsync();
        await using var facade = await Hosts.StartFacadeAsync(legacy.Client.BaseAddress!);

        var flip = await facade.Client.PutAsJsonAsync(new Uri("/bridge/flags/arrears", UriKind.Relative), new { mode = "New" });

        Assert.Equal(HttpStatusCode.BadRequest, flip.StatusCode);
    }

    [Fact]
    public async Task Runtime_flag_endpoint_is_off_unless_enabled()
    {
        await using var legacy = await Hosts.StartLegacyAsync();
        await using var facade = await Hosts.StartFacadeAsync(legacy.Client.BaseAddress!,
            new Dictionary<string, string?> { ["Bridge:AllowRuntimeFlagChanges"] = "false" });

        var flip = await facade.Client.PutAsJsonAsync(new Uri("/bridge/flags/customers", UriKind.Relative), new { mode = "New" });

        Assert.Equal(HttpStatusCode.NotFound, flip.StatusCode);
    }

    [Fact]
    public async Task Unmigrated_capabilities_are_forwarded_byte_for_byte()
    {
        await using var legacy = await Hosts.StartLegacyAsync();
        await using var facade = await Hosts.StartFacadeAsync(legacy.Client.BaseAddress!);

        var viaFacade = await facade.Client.GetAsync(new Uri("/schedules/LN-200310", UriKind.Relative));
        var direct = await legacy.Client.GetStringAsync(new Uri("/TERM/SCHED/LN-200310", UriKind.Relative));

        Assert.Equal("legacy", Header(viaFacade, "X-Served-By"));
        Assert.Equal(direct, await viaFacade.Content.ReadAsStringAsync());
        Assert.StartsWith("text/plain", viaFacade.Content.Headers.ContentType!.ToString());
    }

    [Fact]
    public async Task Shadow_mode_serves_legacy_and_records_a_match()
    {
        await using var legacy = await Hosts.StartLegacyAsync();
        await using var facade = await Hosts.StartFacadeAsync(legacy.Client.BaseAddress!, Mode("customers", "Shadow"));

        var response = await facade.Client.GetAsync(new Uri("/customers/10105", UriKind.Relative));
        var status = await Hosts.EventuallyAsync(
            () => facade.Client.GetStringAsync(new Uri("/bridge/status", UriKind.Relative)),
            s => s.Contains("\"matched\":1", StringComparison.Ordinal));

        Assert.Equal("legacy", Header(response, "X-Served-By"));
        Assert.Equal("Shadow", Header(response, "X-Bridge-Mode"));
        Assert.Contains("\"matched\":1", status);
        Assert.Contains("\"mismatched\":0", status);
    }

    [Fact]
    public async Task Shadow_mismatch_is_logged_with_field_names_but_no_personal_data()
    {
        var logs = new LogCapture();
        await using var legacy = await Hosts.StartLegacyAsync();
        await using var facade = await Hosts.StartFacadeAsync(
            legacy.Client.BaseAddress!,
            Mode("customers", "Shadow"),
            services => services.AddSingleton<ICustomerDirectory>(new DivergingDirectory()),
            logs);

        await facade.Client.GetAsync(new Uri("/customers/10042", UriKind.Relative));
        var status = await Hosts.EventuallyAsync(
            () => facade.Client.GetStringAsync(new Uri("/bridge/status", UriKind.Relative)),
            s => s.Contains("\"mismatched\":1", StringComparison.Ordinal));

        Assert.Contains("\"mismatched\":1", status);
        var mismatch = Assert.Single(logs.Messages, m => m.Contains("Shadow mismatch", StringComparison.Ordinal));
        Assert.Contains("familyName", mismatch);
        Assert.DoesNotContain("Calloway", mismatch, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Kalloway", mismatch, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("10042", mismatch);
        Assert.All(logs.Messages, m => Assert.DoesNotContain("1985", m));
    }

    [Fact]
    public async Task Failing_legacy_returns_503_and_opens_the_breaker()
    {
        await using var legacy = await Hosts.StartLegacyAsync();
        await using var facade = await Hosts.StartFacadeAsync(legacy.Client.BaseAddress!,
            new Dictionary<string, string?> { ["Bridge:Legacy:MaxRetries"] = "1", ["Bridge:Legacy:BreakerFailureThreshold"] = "2" });
        await legacy.Client.PutAsync(new Uri("/TERM/FAULT/ERROR", UriKind.Relative), null);

        var response = await facade.Client.GetAsync(new Uri("/customers/10042", UriKind.Relative));
        var status = await facade.Client.GetFromJsonAsync<JsonElement>(new Uri("/bridge/status", UriKind.Relative));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("legacy", Header(response, "X-Served-By"));
        Assert.Equal("Open", status.GetProperty("breaker").GetString());
    }

    [Fact]
    public async Task Health_endpoints_answer()
    {
        await using var legacy = await Hosts.StartLegacyAsync();
        await using var facade = await Hosts.StartFacadeAsync(legacy.Client.BaseAddress!);

        Assert.Equal(HttpStatusCode.OK, (await facade.Client.GetAsync(new Uri("/health/live", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await facade.Client.GetAsync(new Uri("/health/ready", UriKind.Relative))).StatusCode);
    }

    private sealed class DivergingDirectory : ICustomerDirectory
    {
        private static readonly CustomerDetails Drifted =
            new("10042", "Ines", "Kalloway", new DateOnly(1985, 3, 12), "Active", "Retail", new DateOnly(2015, 6, 1), "B014");

        public Task<Agentic.Blocks.Core.Result<CustomerDetails>> FindAsync(string customerNumber, CancellationToken ct = default) =>
            Task.FromResult<Agentic.Blocks.Core.Result<CustomerDetails>>(Drifted);

        public Task<IReadOnlyList<CustomerDetails>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CustomerDetails>>([Drifted]);
    }
}
