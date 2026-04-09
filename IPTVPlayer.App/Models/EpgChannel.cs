namespace IPTVPlayer.App.Models;

public sealed class EpgChannel
{
    public string Id { get; set; } = string.Empty;

    public List<string> DisplayNames { get; set; } = new();
}
