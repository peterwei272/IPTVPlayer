namespace IPTVPlayer.App.Models;

public sealed class ProgrammeItem
{
    public Guid EpgSourceId { get; set; }

    public string ChannelId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Subtitle { get; set; }

    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset EndUtc { get; set; }

    public string? Description { get; set; }

    public string? Category { get; set; }

    public DateTimeOffset LocalStart => StartUtc.ToLocalTime();

    public DateTimeOffset LocalEnd => EndUtc.ToLocalTime();

    public string ScheduleText => FormatScheduleRange(StartUtc, EndUtc);

    public bool IsCurrent(DateTimeOffset now)
    {
        return StartUtc <= now && now < EndUtc;
    }

    public static string FormatScheduleRange(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        return $"{FormatSchedulePoint(startUtc)} - {FormatSchedulePoint(endUtc)}";
    }

    private static string FormatSchedulePoint(DateTimeOffset value)
    {
        var local = value.ToLocalTime();
        return local.Date == DateTime.Now.Date
            ? local.ToString("HH:mm")
            : local.ToString("MM-dd HH:mm");
    }
}
