using IPTVPlayer.App.Models;

namespace IPTVPlayer.App.Services;

public sealed class AppDataSnapshot
{
    public List<ChannelItem> Channels { get; init; } = new();

    public Dictionary<string, List<ProgrammeItem>> ProgrammeLookup { get; init; } = new(StringComparer.Ordinal);

    public Dictionary<Guid, EpgCacheRecord> EpgCaches { get; init; } = new();

    public static string ComposeProgrammeKey(Guid sourceId, string channelId)
    {
        return $"{sourceId:N}|{channelId}";
    }
}
