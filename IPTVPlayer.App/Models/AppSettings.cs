namespace IPTVPlayer.App.Models;

public sealed class AppSettings
{
    public List<PlaylistSource> PlaylistSources { get; set; } = new();

    public List<EpgSource> EpgSources { get; set; } = new();

    public List<string> FavoriteChannelKeys { get; set; } = new();

    public List<string> RecentChannelKeys { get; set; } = new();

    public string? LastPlayedChannelKey { get; set; }

    public string? LastSelectedChannelKey { get; set; }
}
