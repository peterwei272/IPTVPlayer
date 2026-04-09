namespace IPTVPlayer.App.Services.Media;

public sealed class VideoSignalInfo
{
    public string BadgeText { get; init; } = "未播放";

    public string DetailText { get; init; } = "等待选择频道";

    public string DynamicRange { get; init; } = "未播放";

    public string? Resolution { get; init; }

    public string? FrameRate { get; init; }

    public string? AudioChannels { get; init; }

    public string? AudioCodec { get; init; }

    public string? AudioSampleRate { get; init; }

    public string? ColorPrimaries { get; init; }

    public string? TechnicalSummary { get; init; }
}
