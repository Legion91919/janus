using Agentic.Blocks.Core;
using Agentic.Blocks.Web;
using Janus.Facade.Legacy;
using Janus.Facade.Routing;
using Janus.Facade.Shadow;
using Microsoft.Extensions.Options;

namespace Janus.Facade;

/// <summary>
/// The strangler-fig facade: a small reverse proxy. For each capability path it reads the flag and either forwards
/// to the old terminal (Legacy), serves from the new module (New), or forwards and compares in the background (Shadow).
/// Every routed response says who served it in <c>X-Served-By</c> and which mode applied in <c>X-Bridge-Mode</c>.
/// </summary>
internal sealed class BridgeMiddleware(RequestDelegate next, IOptionsMonitor<BridgeOptions> options, ShadowQueue shadow)
{
    public const string ServedByHeader = "X-Served-By";
    public const string ModeHeader = "X-Bridge-Mode";

    public async Task InvokeAsync(HttpContext context)
    {
        if (CapabilityCatalog.Match(context.Request.Path.Value ?? "/") is not { } match)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var (capability, rest) = match;
        var services = context.RequestServices;
        var newSide = services.GetServices<INewCapability>().FirstOrDefault(c => c.Name == capability.Name);
        var mode = newSide is null ? RoutingMode.Legacy : options.CurrentValue.ModeFor(capability.Name);
        var isRead = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
        context.Response.Headers[ModeHeader] = mode.ToString();

        if (mode == RoutingMode.New)
        {
            context.Response.Headers[ServedByHeader] = "new";
            var result = isRead
                ? await newSide!.GetAsync(rest, context.RequestAborted).ConfigureAwait(false)
                : new CapabilityResult(StatusCodes.Status405MethodNotAllowed, null, null);
            await WriteAsync(context, result).ConfigureAwait(false);
            return;
        }

        context.Response.Headers[ServedByHeader] = "legacy";
        var correlationId = services.GetService<ICorrelationContext>()?.CorrelationId;
        LegacyResponse legacy;
        try
        {
            var body = isRead ? null : await ReadBodyAsync(context.Request).ConfigureAwait(false);
            legacy = await services.GetRequiredService<LegacyClient>().SendAsync(
                context.Request.Method, capability.LegacyPrefix + rest + context.Request.QueryString, body,
                context.Request.ContentType, correlationId, context.RequestAborted).ConfigureAwait(false);
        }
        catch (LegacyUnavailableException ex)
        {
            if (ex.Reason == "circuit-open") context.Response.Headers.RetryAfter = options.CurrentValue.Legacy.BreakerOpenSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await Results.Problem(
                title: "The old servicing system is unavailable",
                detail: $"Reason: {ex.Reason}. Try again shortly.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                extensions: new Dictionary<string, object?> { ["code"] = "bridge.legacy_unavailable", ["reason"] = ex.Reason })
                .ExecuteAsync(context).ConfigureAwait(false);
            return;
        }

        if (newSide is null || !isRead)
        {
            // Not migrated yet: the facade is transparent and passes the old terminal's answer through untouched.
            context.Response.StatusCode = legacy.StatusCode;
            context.Response.ContentType = legacy.ContentType;
            await context.Response.WriteAsync(legacy.Body, context.RequestAborted).ConfigureAwait(false);
            return;
        }

        var translated = newSide.TranslateLegacy(rest, legacy);
        await WriteAsync(context, translated).ConfigureAwait(false);
        if (mode == RoutingMode.Shadow) shadow.TryEnqueue(new ShadowWork(capability.Name, rest, translated, correlationId));
    }

    private static Task WriteAsync(HttpContext context, CapabilityResult result) =>
        (result switch
        {
            { Error: { } error } => error.ToProblem(),
            { Body: null } => Results.StatusCode(result.StatusCode),
            _ => Results.Json(result.Body, statusCode: result.StatusCode),
        }).ExecuteAsync(context);

    private static async Task<byte[]> ReadBodyAsync(HttpRequest request)
    {
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, request.HttpContext.RequestAborted).ConfigureAwait(false);
        return buffer.ToArray();
    }
}
