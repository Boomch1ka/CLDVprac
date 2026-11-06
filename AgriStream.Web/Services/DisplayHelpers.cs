namespace AgriStream.Web.Services;

public static class DisplayHelpers
{
    public static string TimeAgo(this DateTimeOffset value)
    {
        if (value == DateTimeOffset.MinValue) return "-";
        var span = DateTimeOffset.UtcNow - value;
        if (span.TotalSeconds < 0) return "in " + Describe(span.Negate());
        return Describe(span) + " ago";
    }

    public static string TimeAgo(this DateTime? utcValue) =>
        utcValue.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(utcValue.Value, DateTimeKind.Utc)).TimeAgo() : "never";

    private static string Describe(TimeSpan s) =>
        s.TotalSeconds < 60 ? $"{(int)s.TotalSeconds}s" :
        s.TotalMinutes < 60 ? $"{(int)s.TotalMinutes} min" :
        s.TotalHours < 24 ? $"{(int)s.TotalHours} h" :
        $"{(int)s.TotalDays} d";

    public static string FileSize(this long bytes) =>
        bytes < 1024 ? $"{bytes} B" :
        bytes < 1024 * 1024 ? $"{bytes / 1024.0:0.#} KB" :
        $"{bytes / (1024.0 * 1024.0):0.#} MB";

    public static (string Css, string Emoji, string Label) MoistureMood(double moisture) =>
        moisture < 25 ? ("dry", "🥵", "Thirsty") :
        moisture > 65 ? ("wet", "💦", "Soggy") :
        ("good", "😊", "Happy");

    public static string StatusCss(string status) => status switch
    {
        "Irrigating" => "status-irrigating",
        "Maintenance" => "status-maintenance",
        "Offline" => "status-offline",
        _ => "status-online"
    };
}
