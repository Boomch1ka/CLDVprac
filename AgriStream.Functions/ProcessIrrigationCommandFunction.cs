using AgriStream.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions;

/// <summary>
/// (4) QUEUE TRIGGER
/// Listens to the "irrigation-commands" Storage Queue, processes each command and updates the
/// device's operational status in the Azure SQL "Devices" master registry table.
/// Also logs the irrigation event to the "IrrigationEvents" table via a Table OUTPUT binding.
/// If SQL is unavailable the function throws, the message becomes visible again and is retried
/// (up to maxDequeueCount in host.json) before moving to the poison queue - nothing is lost.
/// </summary>
public class ProcessIrrigationCommandFunction(IConfiguration config, ILogger<ProcessIrrigationCommandFunction> logger)
{
    private const string UpdateSql = @"
UPDATE dbo.Devices
SET Status = N'Irrigating',
    LastIrrigatedAt = @now,
    IrrigationEndsAt = @endsAt,
    LastCommandBy = @requestedBy,
    UpdatedAt = @now
WHERE DeviceId = @deviceId;";

    [Function("ProcessIrrigationCommand")]
    [TableOutput("IrrigationEvents", Connection = "AzureWebJobsStorage")]
    public async Task<IrrigationEventEntity> Run(
        [QueueTrigger("irrigation-commands", Connection = "AzureWebJobsStorage")] IrrigationCommand command)
    {
        var connectionString = config["SqlConnectionString"]
            ?? throw new InvalidOperationException("SqlConnectionString app setting is missing.");

        var now = DateTime.UtcNow;
        int rows;

        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var cmd = new SqlCommand(UpdateSql, connection);
            cmd.Parameters.AddWithValue("@now", now);
            cmd.Parameters.AddWithValue("@endsAt", now.AddMinutes(command.DurationMinutes));
            cmd.Parameters.AddWithValue("@requestedBy", command.RequestedBy.Length > 50 ? command.RequestedBy[..50] : command.RequestedBy);
            cmd.Parameters.AddWithValue("@deviceId", command.DeviceId);
            rows = await cmd.ExecuteNonQueryAsync();
        }

        var result = rows > 0 ? "Completed" : "DeviceNotFound";
        logger.LogInformation("Irrigation command {CommandId} for {DeviceId}: {Result}", command.CommandId, command.DeviceId, result);

        var processedAt = DateTimeOffset.UtcNow;
        return new IrrigationEventEntity
        {
            PartitionKey = command.DeviceId,
            RowKey = Keys.NewestFirst(processedAt),
            CommandId = command.CommandId,
            DeviceId = command.DeviceId,
            DurationMinutes = command.DurationMinutes,
            RequestedBy = command.RequestedBy,
            Result = result,
            RequestedAt = command.RequestedAt,
            ProcessedAt = processedAt
        };
    }
}
