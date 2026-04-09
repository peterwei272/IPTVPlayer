namespace IPTVPlayer.App.Models;

public sealed class ChannelItem
{

    public string ChannelKey { get; set; } = string.Empty;

    public Guid SourceId { get; set; }

    public string SourceName { get; set; } = string.Empty;

    public int PlaylistOrder { get; set; }

    public string GroupTitle { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? TvgName { get; set; }

    public string? TvgId { get; set; }

    public string? LogoUrl { get; set; }

    public string StreamUrl { get; set; } = string.Empty;

    public Dictionary<string, string> RawAttributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Guid? MatchedEpgSourceId { get; set; }

    public string? MatchedEpgChannelId { get; set; }

    public ChannelMatchResult? MatchResult { get; set; }

    public string SearchText => $"{DisplayName} {TvgName} {GroupTitle}";
}
