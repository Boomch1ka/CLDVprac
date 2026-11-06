using AgriStream.Web.Models;
using Azure.Data.Tables;

namespace AgriStream.Web.Services;

/// <summary>Reads the audit rows that the Azure Functions write into Table Storage.</summary>
public class AuditService
{
    private readonly TableClient _uploads;
    private readonly TableClient _irrigation;

    public AuditService(TableServiceClient tables, IConfiguration config)
    {
        _uploads = tables.GetTableClient(config["Storage:UploadAuditTable"] ?? "UploadAudit");
        _irrigation = tables.GetTableClient(config["Storage:IrrigationEventsTable"] ?? "IrrigationEvents");
    }

    public async Task EnsureCreatedAsync()
    {
        await _uploads.CreateIfNotExistsAsync();
        await _irrigation.CreateIfNotExistsAsync();
    }

    public async Task<List<UploadAuditEntry>> GetUploadAuditAsync(int max = 100, CancellationToken ct = default)
    {
        var rows = new List<UploadAuditEntry>();
        await foreach (var e in _uploads.QueryAsync<TableEntity>(maxPerPage: 1000, cancellationToken: ct))
        {
            rows.Add(new UploadAuditEntry
            {
                BlobName = e.Str("BlobName"),
                SizeBytes = e.Lng("SizeBytes"),
                ContentType = e.Str("ContentType"),
                UploadedBy = e.Str("UploadedBy"),
                FieldName = e.Str("FieldName"),
                Status = e.Str("Status"),
                Message = e.Str("Message"),
                ProcessedAt = e.Date("ProcessedAt")
            });
            if (rows.Count >= 2000) break;
        }
        return rows.OrderByDescending(r => r.ProcessedAt).Take(max).ToList();
    }

    public async Task<List<IrrigationEventEntry>> GetIrrigationEventsAsync(int max = 100, CancellationToken ct = default)
    {
        var rows = new List<IrrigationEventEntry>();
        await foreach (var e in _irrigation.QueryAsync<TableEntity>(maxPerPage: 1000, cancellationToken: ct))
        {
            rows.Add(new IrrigationEventEntry
            {
                CommandId = e.Str("CommandId"),
                DeviceId = e.Str("DeviceId"),
                DurationMinutes = (int)e.Lng("DurationMinutes"),
                RequestedBy = e.Str("RequestedBy"),
                Result = e.Str("Result"),
                RequestedAt = e.Date("RequestedAt"),
                ProcessedAt = e.Date("ProcessedAt")
            });
            if (rows.Count >= 2000) break;
        }
        return rows.OrderByDescending(r => r.ProcessedAt).Take(max).ToList();
    }
}
