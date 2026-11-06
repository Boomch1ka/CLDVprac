using System.Text;
using System.Text.RegularExpressions;
using AgriStream.Web.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace AgriStream.Web.Services;

/// <summary>Stores drone images / farm media in Azure Blob Storage.</summary>
public class BlobMediaService
{
    private readonly BlobContainerClient _container;

    public BlobMediaService(BlobServiceClient blobs, IConfiguration config)
    {
        _container = blobs.GetBlobContainerClient(config["Storage:DroneContainer"] ?? "drone-images");
    }

    public Task EnsureCreatedAsync() => _container.CreateIfNotExistsAsync(PublicAccessType.None);

    public async Task<string> UploadAsync(Stream content, string originalFileName, string contentType,
        string uploadedBy, string fieldName, string? notes, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var slug = Regex.Replace(fieldName.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrEmpty(slug)) slug = "field";

        var blobName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{slug}-{Guid.NewGuid().ToString("N")[..8]}{ext}";
        var blob = _container.GetBlobClient(blobName);

        // Metadata is read by the Blob-triggered Azure Function and written to the audit table.
        var metadata = new Dictionary<string, string>
        {
            ["uploadedBy"] = Ascii(uploadedBy),
            ["fieldName"] = Ascii(fieldName),
            ["originalName"] = Ascii(Path.GetFileName(originalFileName)),
            ["notes"] = Ascii(notes ?? string.Empty)
        };

        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            Metadata = metadata
        }, ct);

        return blobName;
    }

    public async Task<List<DroneImageItem>> ListAsync(int max = 60, CancellationToken ct = default)
    {
        var items = new List<DroneImageItem>();
        await foreach (var b in _container.GetBlobsAsync(BlobTraits.Metadata, cancellationToken: ct))
        {
            items.Add(new DroneImageItem
            {
                Name = b.Name,
                Url = GetReadUrl(b.Name),
                SizeBytes = b.Properties.ContentLength ?? 0,
                UploadedAt = b.Properties.CreatedOn ?? DateTimeOffset.MinValue,
                UploadedBy = b.Metadata.TryGetValue("uploadedBy", out var by) ? by : "unknown",
                FieldName = b.Metadata.TryGetValue("fieldName", out var f) ? f : "-",
                Notes = b.Metadata.TryGetValue("notes", out var n) ? n : null
            });
            if (items.Count >= 1000) break;
        }
        return items.OrderByDescending(i => i.UploadedAt).Take(max).ToList();
    }

    public async Task<(Stream Content, string ContentType)?> OpenReadAsync(string name, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(name);
        if (!await blob.ExistsAsync(ct)) return null;
        var download = await blob.DownloadStreamingAsync(cancellationToken: ct);
        return (download.Value.Content, download.Value.Details.ContentType ?? "application/octet-stream");
    }

    /// <summary>Short-lived read-only SAS link (container stays private). Falls back to a proxy action.</summary>
    private string GetReadUrl(string name)
    {
        var blob = _container.GetBlobClient(name);
        return blob.CanGenerateSasUri
            ? blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(1)).ToString()
            : $"/Drone/Image?name={Uri.EscapeDataString(name)}";
    }

    private static string Ascii(string value)
    {
        var sb = new StringBuilder();
        foreach (var c in value)
        {
            if (c >= 32 && c < 127) sb.Append(c);
        }
        var s = sb.ToString().Trim();
        return s.Length > 200 ? s[..200] : s;
    }
}
