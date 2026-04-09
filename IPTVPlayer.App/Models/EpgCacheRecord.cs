namespace IPTVPlayer.App.Models;

public sealed class EpgCacheRecord
{
    public Guid SourceId { get; set; }

    public DateTimeOffset RefreshedAt { get; set; }

    public List<EpgChannel> Channels { get; set; } = new();

    public List<ProgrammeItem> Programmes { get; set; } = new();
}
