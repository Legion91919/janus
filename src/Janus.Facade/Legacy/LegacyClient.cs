using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace Janus.Facade.Legacy;

internal sealed record LegacyResponse(int StatusCode, string ContentType, string Body);

/// <summary>Forwards one request to the old terminal through the resilient caller. Only GET/HEAD are retried.</summary>
internal sealed class LegacyClient(HttpClient http, ResilientCaller caller, IOptionsMonitor<BridgeOptions> options)
{
    public Task<LegacyResponse> SendAsync(string method, string pathAndQuery, byte[]? body, string? contentType, string? correlationId, CancellationToken ct)
    {
        var legacy = options.CurrentValue.Legacy;
        var idempotent = HttpMethods.IsGet(method) || HttpMethods.IsHead(method);
        var target = new Uri(legacy.BaseAddress, pathAndQuery);
        return caller.ExecuteAsync(idempotent ? legacy : legacy.WithoutRetries(), async token =>
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), target);
            if (correlationId is not null) request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
            if (body is { Length: > 0 })
            {
                request.Content = new ByteArrayContent(body);
                if (contentType is not null) request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            }

            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            return new LegacyResponse((int)response.StatusCode, response.Content.Headers.ContentType?.ToString() ?? "text/plain", text);
        }, r => r.StatusCode >= 500 || r.StatusCode is 408 or 429, ct);
    }
}
