using Azure.Data.Tables;

namespace AgriStream.Web.Services;

/// <summary>
/// Defensive readers for Table entities. Rows written through Function output bindings may store
/// numbers/dates with slightly different EDM types, so we convert loosely.
/// </summary>
public static class EntityHelpers
{
    public static string Str(this TableEntity e, string key) =>
        e.TryGetValue(key, out var v) && v is not null ? v.ToString() ?? string.Empty : string.Empty;

    public static double Dbl(this TableEntity e, string key) =>
        e.TryGetValue(key, out var v) && v is not null && double.TryParse(v.ToString(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;

    public static long Lng(this TableEntity e, string key) => (long)Math.Round(e.Dbl(key));

    public static DateTimeOffset Date(this TableEntity e, string key)
    {
        if (e.TryGetValue(key, out var v) && v is not null)
        {
            switch (v)
            {
                case DateTimeOffset dto: return dto;
                case DateTime dt: return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
                default:
                    if (DateTimeOffset.TryParse(v.ToString(), System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
                        return parsed;
                    break;
            }
        }
        return e.Timestamp ?? DateTimeOffset.MinValue;
    }

    /// <summary>Row key that sorts newest-first inside a partition.</summary>
    public static string NewestFirstRowKey(DateTimeOffset at) =>
        $"{DateTime.MaxValue.Ticks - at.UtcTicks:D19}_{Guid.NewGuid():N}";
}
