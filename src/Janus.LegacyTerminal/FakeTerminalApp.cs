using System.Globalization;
using System.Text;

namespace Janus.LegacyTerminal;

/// <summary>
/// A stand-in for the old servicing terminal. Treat it as the system nobody can change: fixed-width ASCII records,
/// two-letter upper-case codes, CYYMMDD dates (C = 0 for 19xx, 1 for 20xx), money as 11 digits with two implied
/// decimals, and errors reported as HTTP 200 with an "ERR nnnn" line.
/// </summary>
public static class FakeTerminalApp
{
    public static IServiceCollection AddFakeTerminal(this IServiceCollection services)
    {
        services.AddSingleton<TerminalData>();
        services.AddSingleton<FaultSwitch>();
        return services;
    }

    public static WebApplication MapFakeTerminal(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Use(async (context, next) =>
        {
            var faults = context.RequestServices.GetRequiredService<FaultSwitch>();
            if (!context.Request.Path.StartsWithSegments("/TERM/FAULT", StringComparison.Ordinal))
            {
                if (faults.Mode == "ERROR") { context.Response.StatusCode = 500; await context.Response.WriteAsync("SYS 9999 ABEND"); return; }
                if (faults.Mode == "SLOW") await Task.Delay(TimeSpan.FromSeconds(5), context.RequestAborted);
            }

            await next();
        });

        app.MapGet("/", () => Text("TERMINAL SERVICING SYSTEM R4.2 READY"));
        app.MapGet("/TERM/CUST", (TerminalData d) => Text(Records("CUSTLIST", d.Customers)));
        app.MapGet("/TERM/CUST/{custNo}", (string custNo, TerminalData d) =>
        {
            if (custNo.Length > 8 || !custNo.All(char.IsAsciiDigit)) return Text("ERR 0400 INVALID CUSTNO");
            var key = custNo.PadLeft(8, '0');
            var line = d.Customers.FirstOrDefault(c => c.AsSpan(4, 8).SequenceEqual(key));
            return Text(line is null ? "ERR 0404 CUSTOMER NOT FOUND" : line + "\nEOR 0001 RECORDS\n");
        });
        app.MapGet("/TERM/SCHED/{loanNo}", (string loanNo, TerminalData d) =>
        {
            var lines = d.Schedules.Where(s => s.AsSpan(4, 10).Trim().SequenceEqual(loanNo.ToUpperInvariant())).ToList();
            return Text(lines.Count == 0 ? "ERR 0404 LOAN NOT FOUND" : Records("SCHEDULE", lines));
        });
        app.MapGet("/TERM/ARREARS", (TerminalData d) => Text(Records("ARREARS", d.Arrears)));
        app.MapPut("/TERM/FAULT/{mode}", (string mode, FaultSwitch f) =>
        {
            f.Mode = mode.ToUpperInvariant();
            return Text("OK FAULT " + f.Mode);
        });
        return app;
    }

    private static IResult Text(string body) => Results.Text(body, "text/plain", Encoding.ASCII);

    private static string Records(string name, IReadOnlyCollection<string> lines)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"HDR {name} 1261120\n");
        foreach (var line in lines) sb.Append(line).Append('\n');
        sb.Append(CultureInfo.InvariantCulture, $"EOR {lines.Count:D4} RECORDS\n");
        return sb.ToString();
    }
}

internal sealed class FaultSwitch
{
    public volatile string Mode = "NONE";
}

/// <summary>Invented records in the terminal's own layout. Column positions are the "copybook".</summary>
internal sealed class TerminalData
{
    // REC(3) CUSTNO(8) SURNAME(20) GIVEN(15) DOB(7 CYYMMDD) STATUS(2) SEGMENT(2) SINCE(7 CYYMMDD) BRANCH(4)
    public IReadOnlyList<string> Customers { get; } =
    [
        Cust(10042, "CALLOWAY", "INES", "0850312", "AC", "RT", "1150601", "B014"),
        Cust(10077, "RENNER", "TOBIAS", "0791130", "AC", "SM", "1090217", "B003"),
        Cust(10105, "FOURIE", "NALEDI", "1010704", "DO", "RT", "1210909", "B014"),
        Cust(10213, "ASHBY-LINDQVIST", "KWAME", "0680122", "CL", "RT", "0980430", "B007"),
        Cust(10388, "VELLUM", "PRIYA", "0921205", "AC", "SM", "1181015", "B003"),
        Cust(10450, "BRANDT", "OSKAR", "0550519", "AC", "RT", "0870803", "B001"),
    ];

    // REC(3) LOANNO(10) INST(3) DUE(7 CYYMMDD) AMOUNT(11, 2 implied decimals) STATUS(2: PD paid, DU due, OD overdue)
    public IReadOnlyList<string> Schedules { get; } =
    [
        "SCH LN-200310 001 1261015 00000452350 PD",
        "SCH LN-200310 002 1261115 00000452350 OD",
        "SCH LN-200310 003 1261215 00000452350 DU",
        "SCH LN-200477 001 1261101 00001210000 PD",
        "SCH LN-200477 002 1261201 00001210000 DU",
    ];

    // REC(3) LOANNO(10) CUSTNO(8) DAYSPD(4) ARREARS(11, 2 implied decimals) BUCKET(2: B1 1-30, B2 31-60, B3 61-90, B4 90+)
    public IReadOnlyList<string> Arrears { get; } =
    [
        "ARR LN-200310 00010042 0005 00000452350 B1",
        "ARR LN-199822 00010213 0074 00002875000 B3",
    ];

    private static string Cust(int no, string surname, string given, string dob, string status, string segment, string since, string branch) =>
        string.Create(CultureInfo.InvariantCulture, $"CUS {no:D8} {surname,-20} {given,-15} {dob} {status} {segment} {since} {branch}");
}
