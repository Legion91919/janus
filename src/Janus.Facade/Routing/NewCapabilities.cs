using Agentic.Blocks.Core;
using Janus.Customers;
using Janus.Facade.Legacy;

namespace Janus.Facade.Routing;

/// <summary>The outcome of serving a capability request from either side, in the modern contract.</summary>
internal sealed record CapabilityResult(int StatusCode, object? Body, Error? Error)
{
    public static CapabilityResult From<T>(Result<T> result) => result.IsSuccess
        ? new(StatusCodes.Status200OK, result.Value, null)
        : new(Agentic.Blocks.Web.ErrorHttpMapping.StatusCodeFor(result.Error.Type), null, result.Error);
}

/// <summary>A capability that has a new implementation: it can serve requests and translate the legacy answer into the same contract.</summary>
internal interface INewCapability
{
    string Name { get; }

    Task<CapabilityResult> GetAsync(string rest, CancellationToken ct);

    CapabilityResult TranslateLegacy(string rest, LegacyResponse legacy);
}

/// <summary>Customers: the first capability moved. "" lists, "/{number}" finds one.</summary>
internal sealed class CustomersCapability(ICustomerDirectory directory, ILegacyCustomerTranslator translator) : INewCapability
{
    private static readonly Error NoSuchRoute = Error.NotFound("bridge.no_route", "No such customers route.");

    public string Name => "customers";

    public async Task<CapabilityResult> GetAsync(string rest, CancellationToken ct)
    {
        if (rest.Length == 0) return CapabilityResult.From(Result.Success(await directory.ListAsync(ct).ConfigureAwait(false)));
        if (Id(rest) is not { } id) return CapabilityResult.From(Result.Failure<object>(NoSuchRoute));
        return CapabilityResult.From(await directory.FindAsync(id, ct).ConfigureAwait(false));
    }

    public CapabilityResult TranslateLegacy(string rest, LegacyResponse legacy)
    {
        if (legacy.StatusCode == StatusCodes.Status404NotFound) return CapabilityResult.From(Result.Failure<object>(NoSuchRoute));
        if (rest.Length == 0) return CapabilityResult.From(translator.TranslateMany(legacy.Body));
        if (Id(rest) is null) return CapabilityResult.From(Result.Failure<object>(NoSuchRoute));
        return CapabilityResult.From(translator.TranslateOne(legacy.Body));
    }

    private static string? Id(string rest)
    {
        var id = rest.TrimStart('/');
        return id.Length > 0 && !id.Contains('/', StringComparison.Ordinal) ? Uri.UnescapeDataString(id) : null;
    }
}
