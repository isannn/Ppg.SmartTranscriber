using System.Net.Http.Headers;
using System.Text.Json;

namespace SmartTranscriber.Infrastructure.Infisical;

internal sealed class InfisicalSecretsClient
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private readonly HttpMessageHandler? _handler;

    public InfisicalSecretsClient(HttpMessageHandler? handler = null)
    {
        _handler = handler;
    }

    public async Task<IReadOnlyDictionary<string, string?>> FetchAsync(
        InfisicalOptions options, CancellationToken ct = default)
    {
        var url = $"{options.Url}/api/v3/secrets/raw" +
                  $"?workspaceId={Uri.EscapeDataString(options.ProjectId)}" +
                  $"&environment={Uri.EscapeDataString(options.Environment)}" +
                  $"&secretPath={Uri.EscapeDataString(options.SecretPath)}";

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(Timeout);

        using var client = _handler is null
            ? new HttpClient()
            : new HttpClient(_handler, disposeHandler: false);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Infisical fetch failed ({options.Url}): {ex.Message}", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Infisical fetch failed ({options.Url}): HTTP {(int)response.StatusCode}");
            }

            var body = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            try
            {
                return Parse(body);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Infisical response is not valid JSON ({options.Url})", ex);
            }
        }
    }

    internal static Dictionary<string, string?> Parse(string body)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("secrets", out var secrets))
        {
            return result;
        }

        foreach (var s in secrets.EnumerateArray())
        {
            var key = s.GetProperty("secretKey").GetString();
            if (key is null)
            {
                continue;
            }

            result[key.Replace("__", ":")] = s.GetProperty("secretValue").GetString();
        }

        return result;
    }
}
