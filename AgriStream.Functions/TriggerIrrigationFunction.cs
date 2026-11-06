using System.Net;
using System.Text.Json;
using AgriStream.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions;

/// <summary>
/// (3) HTTP TRIGGER - POST
/// Called when a user clicks "Trigger Irrigation" in the MVC app.
/// Validates the request and places an "Irrigation Command" on the "irrigation-commands"
/// Storage Queue using a Queue OUTPUT binding.
/// POST /api/irrigation/trigger  { "deviceId": "SENSOR-001", "durationMinutes": 10, "requestedBy": "farmer_joe" }
/// </summary>
public class TriggerIrrigationFunction(ILogger<TriggerIrrigationFunction> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Function("TriggerIrrigation")]
    public async Task<TriggerIrrigationOutput> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "irrigation/trigger")] HttpRequestData req)
    {
        IrrigationRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<IrrigationRequest>(req.Body, JsonOptions);
        }
        catch (JsonException)
        {
            body = null;
        }

        if (body is null || string.IsNullOrWhiteSpace(body.DeviceId) || body.DurationMinutes is < 1 or > 120)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteAsJsonAsync(new { error = "deviceId is required and durationMinutes must be between 1 and 120." });
            return new TriggerIrrigationOutput { HttpResponse = bad };
        }

        var command = new IrrigationCommand
        {
            DeviceId = body.DeviceId.Trim(),
            DurationMinutes = body.DurationMinutes,
            RequestedBy = string.IsNullOrWhiteSpace(body.RequestedBy) ? "unknown" : body.RequestedBy.Trim(),
            RequestedAt = DateTimeOffset.UtcNow
        };

        logger.LogInformation("Queueing irrigation command {CommandId} for {DeviceId} ({Minutes} min) by {User}",
            command.CommandId, command.DeviceId, command.DurationMinutes, command.RequestedBy);

        var accepted = req.CreateResponse(HttpStatusCode.Accepted);
        await accepted.WriteAsJsonAsync(new { message = "Irrigation command queued.", command.CommandId, command.DeviceId });

        return new TriggerIrrigationOutput { Command = command, HttpResponse = accepted };
    }
}

/// <summary>Multiple outputs: an HTTP response AND a Storage Queue message.</summary>
public class TriggerIrrigationOutput
{
    [QueueOutput("irrigation-commands", Connection = "AzureWebJobsStorage")]
    public IrrigationCommand? Command { get; set; }

    public HttpResponseData HttpResponse { get; set; } = default!;
}
