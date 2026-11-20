using Janus.Facade.Routing;
using Janus.Facade.Shadow;
using Xunit;

namespace Janus.Tests;

public class RoutingTests
{
    [Theory]
    [InlineData("/customers", "customers", "")]
    [InlineData("/customers/10042", "customers", "/10042")]
    [InlineData("/CUSTOMERS/10042", "customers", "/10042")]
    [InlineData("/schedules/LN-200310", "schedules", "/LN-200310")]
    [InlineData("/arrears", "arrears", "")]
    public void Paths_match_their_capability_on_segment_boundaries(string path, string capability, string rest)
    {
        var match = CapabilityCatalog.Match(path);

        Assert.NotNull(match);
        Assert.Equal(capability, match!.Value.Capability.Name);
        Assert.Equal(rest, match.Value.Rest);
    }

    [Theory]
    [InlineData("/customersX")]
    [InlineData("/bridge/status")]
    [InlineData("/")]
    public void Other_paths_are_not_routed(string path) => Assert.Null(CapabilityCatalog.Match(path));

    [Fact]
    public void Legacy_paths_keep_the_rest_of_the_path()
    {
        var match = CapabilityCatalog.Match("/customers/10042")!.Value;

        Assert.Equal("/TERM/CUST/10042", match.Capability.LegacyPrefix + match.Rest);
    }

    [Fact]
    public void Json_diff_reports_paths_only()
    {
        var diffs = JsonDiff.Paths(
            """[{"familyName":"A","branch":"B1"},{"familyName":"C"}]""",
            """[{"familyName":"Z","branch":"B1"},{"familyName":"C"},{"familyName":"D"}]""");

        Assert.Equal(["$[0].familyName", "$.length"], diffs);
    }

    [Fact]
    public void Json_diff_of_equal_documents_is_empty() =>
        Assert.Empty(JsonDiff.Paths("""{"a":1,"b":[1,2]}""", """{"b":[1,2],"a":1}"""));

    [Fact]
    public void Shadow_fingerprint_hides_the_identifier()
    {
        var fingerprint = ShadowComparer.Fingerprint("/10042");

        Assert.Equal(8, fingerprint.Length);
        Assert.DoesNotContain("10042", fingerprint);
        Assert.Equal(fingerprint, ShadowComparer.Fingerprint("/10042"));
    }
}
