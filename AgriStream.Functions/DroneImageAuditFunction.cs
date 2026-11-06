using AgriStream.Functions.Models;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions;

/// <summary>
/// (1) BLOB TRIGGER
/// Fires whenever a drone image lands in the "drone-images" container.
/// Reads the blob properties/metadata and writes an audit row to the "UploadAudit" table
/// using a Table OUTPUT binding (the return value).
/// </summary>
public class DroneImageAuditFunction(ILogger<DroneImageAuditFunction> logger)
{
    private static readonly string[] ImageTypes = { "image/jpeg", "image/png", "image/webp", "image/gif", "image/tiff" };

    [Function(nameof(DroneImageAuditFunction))]
    [TableOutput("UploadAudit", Connection = "AzureWebJobsStorage")]
    public async Task<UploadAuditEntity> Run(
        [BlobTrigger("drone-images/{name}", Connection = "AzureWebJobsStorage")] BlobClient blob,
        string name)
    {
        var properties = (await blob.GetPropertiesAsync()).Value;
        var metadata = properties.Metadata;
        var processedAt = DateTimeOffset.UtcNow;

        var isImage = ImageTypes.Contains(properties.ContentType, StringComparer.OrdinalIgnoreCase);
        var status = isImage ? "Processed" : "Flagged";
        var message = isImage
            ? $"Drone image accepted ({properties.ContentLength} bytes)."
            : $"Unexpected content type '{properties.ContentType}'.";

        string Meta(string key, string fallback) =>
            metadata.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val) ? val : fallback;

        logger.LogInformation("Blob trigger processed {BlobName} ({Size} bytes) uploaded by {User}: {Status}",
            name, properties.ContentLength, Meta("uploadedBy", "unknown"), status);

        return new UploadAuditEntity
        {
            PartitionKey = processedAt.ToString("yyyy-MM"),
            RowKey = Keys.NewestFirst(processedAt),
            BlobName = name,
            SizeBytes = properties.ContentLength,
            ContentType = properties.ContentType ?? "unknown",
            UploadedBy = Meta("uploadedBy", "unknown"),
            FieldName = Meta("fieldName", "-"),
            OriginalName = Meta("originalName", name),
            Status = status,
            Message = message,
            ProcessedAt = processedAt
        };
    }
}
