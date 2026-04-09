namespace IPTVPlayer.App.Services;

public sealed class RefreshResult
{
    public RefreshResult(AppDataSnapshot snapshot, string statusMessage)
    {
        Snapshot = snapshot;
        StatusMessage = statusMessage;
    }

    public AppDataSnapshot Snapshot { get; }

    public string StatusMessage { get; }
}
