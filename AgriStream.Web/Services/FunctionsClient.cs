using AgriStream.Web.Models;

namespace AgriStream.Web.Services;

/// <summary>
/// Calls the HTTP-triggered Azure Functions:
///  - GET  GetLatestTelemetry  (Functions:GetTelemetryUrl or LocalGetTelemetryUrl)
///  - POST TriggerIrrigation   (Functions:TriggerIrrigationUrl or LocalTriggerIrrigationUrl)
/// </summary>
public class FunctionsClient(HttpClient http, IConfiguration config, ILogger<FunctionsClient> logger)
{
    private string? GetUrl => config["Functions:GetTelemetryUrl"];
    private string? LocalGetUrl => config["Functions:LocalGetTelemetryUrl"];
    private string? PostUrl => config["Functions:TriggerIrrigationUrl"];
    private string? LocalPostUrl => config["Functions:LocalTriggerIrrigationUrl"];

    public bool IsTelemetryConfigured => !string.IsNullOrWhiteSpace(GetUrl) || !string.IsNullOrWhiteSpace(LocalGetUrl);
    public bool IsIrrigationConfigured => !string.IsNullOrWhiteSpace(PostUrl) || !string.IsNullOrWhiteSpace(LocalPostUrl);

    public async Task<TelemetrySnapshot?> GetLatestTelemetryAsync(string? deviceId, int hours, CancellationToken ct = default)
    {
        foreach (var candidate in new[] { LocalGetUrl, GetUrl }.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct())
        {
            try
            {
                var url = AppendQuery(candidate!, "hours", hours.ToString());
                if (!string.IsNullOrWhiteSpace(deviceId)) url = AppendQuery(url, "deviceId", deviceId);
                var res = await http.GetFromJsonAsync<TelemetrySnapshot>(url, ct);
                if (res is not null) return res;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Telemetry endpoint {Url} not reachable, trying next", candidate);
            }
        }
        return null;
    }

    public async Task<(bool Ok, string Message)> TriggerIrrigationAsync(IrrigationRequest request, CancellationToken ct = default)
    {
        if (!IsIrrigationConfigured)
            return (false, "The irrigation function URL isn't configured yet (Functions:TriggerIrrigationUrl).");

        foreach (var candidate in new[] { LocalPostUrl, PostUrl }.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct())
        {
            try
            {
                var response = await http.PostAsJsonAsync(candidate, request, ct);
                if (response.IsSuccessStatusCode)
                    return (true, $"💧 Irrigation command for {request.DeviceId} is queued! Status updates in a few seconds.");

                if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500 && response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    return (false, $"The irrigation function said {(int)response.StatusCode}: {body}");
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "TriggerIrrigation endpoint {Url} not reachable, trying next", candidate);
            }
        }

        return (false, "Couldn't reach the irrigation function yet. Make sure AgriStream.Functions is running locally or published to Azure!");
    }

    private static string AppendQuery(string url, string key, string value) =>
        url + (url.Contains('?') ? "&" : "?") + Uri.EscapeDataString(key) + "=" + Uri.EscapeDataString(value);
}
