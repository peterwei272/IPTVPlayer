namespace IPTVPlayer.App.Models;

public sealed class PlaylistCacheRecord
{
    public Guid SourceId { get; set; }

    public DateTimeOffset RefreshedAt { get; set; }

    public List<string> SuggestedEpgUrls { get; set; } = new();

    public List<ChannelItem> Channels { get; set; } = new();
}
