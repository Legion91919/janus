using System.Globalization;
using Agentic.Blocks.Core;

namespace Janus.Customers;

/// <summary>
/// Anti-corruption layer for the old terminal's customer records. Everything legacy-shaped stops here:
/// zero-padded numbers, upper-case names, two-letter codes, CYYMMDD dates and "ERR nnnn" lines sent with HTTP 200.
/// Unknown codes or impossible dates are rejected, never guessed.
/// </summary>
internal sealed class LegacyCustomerTranslator : ILegacyCustomerTranslator
{
    private const int RecordLength = 76;

    private static readonly Dictionary<string, string> Statuses = new(StringComparer.Ordinal)
    {
        ["AC"] = "Active",
        ["DO"] = "Dormant",
        ["CL"] = "Closed",
    };

    private static readonly Dictionary<string, string> Segments = new(StringComparer.Ordinal)
    {
        ["RT"] = "Retail",
        ["SM"] = "SmallBusiness",
    };

    public Result<CustomerDetails> TranslateOne(string legacyBody)
    {
        var many = TranslateMany(legacyBody, requireTrailer: false);
        if (!many.IsSuccess) return many.Error;
        return many.Value.Count == 1 ? many.Value[0] : Errors.Unreadable("expected exactly one customer record");
    }

    public Result<IReadOnlyList<CustomerDetails>> TranslateMany(string legacyBody) => TranslateMany(legacyBody, requireTrailer: true);

    private static Result<IReadOnlyList<CustomerDetails>> TranslateMany(string body, bool requireTrailer)
    {
        var lines = (body ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0) return Errors.Unreadable("empty response");
        if (lines[0].StartsWith("ERR ", StringComparison.Ordinal)) return MapError(lines[0]);

        var customers = new List<CustomerDetails>();
        int? trailerCount = null;
        foreach (var raw in lines)
        {
            if (raw.StartsWith("HDR ", StringComparison.Ordinal)) continue;
            if (raw.StartsWith("EOR ", StringComparison.Ordinal))
            {
                trailerCount = int.TryParse(raw.AsSpan(4, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : -1;
                continue;
            }

            var record = ParseRecord(raw);
            if (!record.IsSuccess) return record.Error;
            customers.Add(record.Value);
        }

        if (trailerCount is { } count && count != customers.Count) return Errors.Unreadable("trailer count does not match the records");
        if (requireTrailer && trailerCount is null) return Errors.Unreadable("missing trailer");
        return customers;
    }

    private static Error MapError(string line) => line.AsSpan(4, Math.Min(4, line.Length - 4)) switch
    {
        "0404" => Errors.NotFound,
        "0400" => Errors.InvalidNumber,
        _ => Errors.Unreadable("unknown legacy error code"),
    };

    private static Result<CustomerDetails> ParseRecord(string line)
    {
        // Trailing spaces are trimmed in transit, so pad back to the copybook length before slicing.
        var r = line.PadRight(RecordLength);
        if (!r.StartsWith("CUS ", StringComparison.Ordinal) || line.Length > RecordLength) return Errors.Unreadable("not a customer record");

        var number = CustomerNumber.Normalise(r.Substring(4, 8));
        var dob = LegacyDate.Parse(r.Substring(50, 7));
        var since = LegacyDate.Parse(r.Substring(64, 7));
        if (!number.IsSuccess || dob is null || since is null) return Errors.Unreadable("bad number or date");
        if (!Statuses.TryGetValue(r.Substring(58, 2), out var status)) return Errors.Unreadable("unknown status code");
        if (!Segments.TryGetValue(r.Substring(61, 2), out var segment)) return Errors.Unreadable("unknown segment code");

        return new CustomerDetails(
            number.Value,
            Names.TitleCase(r.Substring(34, 15)),
            Names.TitleCase(r.Substring(13, 20)),
            dob.Value,
            status,
            segment,
            since.Value,
            r.Substring(72, 4).Trim());
    }
}

/// <summary>CYYMMDD: C is the century (0 = 19xx, 1 = 20xx), then two-digit year, month and day.</summary>
internal static class LegacyDate
{
    public static DateOnly? Parse(string cyymmdd)
    {
        if (cyymmdd.Length != 7 || !cyymmdd.All(char.IsAsciiDigit) || cyymmdd[0] > '1') return null;
        var year = 1900 + ((cyymmdd[0] - '0') * 100) + int.Parse(cyymmdd.AsSpan(1, 2), CultureInfo.InvariantCulture);
        var month = int.Parse(cyymmdd.AsSpan(3, 2), CultureInfo.InvariantCulture);
        var day = int.Parse(cyymmdd.AsSpan(5, 2), CultureInfo.InvariantCulture);
        return month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month) ? null : new DateOnly(year, month, day);
    }
}

internal static class Names
{
    /// <summary>"ASHBY-LINDQVIST" to "Ashby-Lindqvist". Known limit: prefixes like "Mc" or "van" need a lookup table (see DESIGN.md).</summary>
    public static string TitleCase(string upper)
    {
        var chars = upper.Trim().ToLowerInvariant().ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (i == 0 || chars[i - 1] is ' ' or '-' or '\'') chars[i] = char.ToUpperInvariant(chars[i]);
        }

        return new string(chars);
    }
}
