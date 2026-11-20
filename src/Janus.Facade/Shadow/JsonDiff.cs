using System.Text.Json.Nodes;

namespace Janus.Facade.Shadow;

/// <summary>Lists the JSON paths where two documents differ. Paths only: values never leave this class.</summary>
internal static class JsonDiff
{
    public static IReadOnlyList<string> Paths(string left, string right)
    {
        var diffs = new List<string>();
        Walk(JsonNode.Parse(left), JsonNode.Parse(right), "$", diffs);
        return diffs;
    }

    private static void Walk(JsonNode? a, JsonNode? b, string path, List<string> diffs)
    {
        switch (a, b)
        {
            case (JsonObject oa, JsonObject ob):
                foreach (var key in oa.Select(p => p.Key).Union(ob.Select(p => p.Key), StringComparer.Ordinal))
                {
                    if (!oa.ContainsKey(key) || !ob.ContainsKey(key)) diffs.Add($"{path}.{key}");
                    else Walk(oa[key], ob[key], $"{path}.{key}", diffs);
                }

                break;
            case (JsonArray xa, JsonArray xb):
                for (var i = 0; i < Math.Min(xa.Count, xb.Count); i++) Walk(xa[i], xb[i], $"{path}[{i}]", diffs);
                if (xa.Count != xb.Count) diffs.Add($"{path}.length");
                break;
            default:
                if (!JsonNode.DeepEquals(a, b)) diffs.Add(path);
                break;
        }
    }
}
