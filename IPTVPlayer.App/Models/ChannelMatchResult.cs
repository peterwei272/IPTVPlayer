namespace IPTVPlayer.App.Models;

public sealed class ChannelMatchResult
{
    public string Strategy { get; set; } = string.Empty;

    public string SourceValue { get; set; } = string.Empty;

    public string EpgChannelId { get; set; } = string.Empty;
}
