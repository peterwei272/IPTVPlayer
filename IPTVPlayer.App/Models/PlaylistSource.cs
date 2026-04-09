using System.Globalization;
using IPTVPlayer.App.Infrastructure;

namespace IPTVPlayer.App.Models;

public sealed class PlaylistSource : ObservableObject
{
    private string _name = string.Empty;
    private string _url = string.Empty;
    private bool _enabled = true;
    private bool _refreshOnStartup = true;
    private int _priority;
    private DateTimeOffset? _lastRefreshAt;
    private string? _eTag;
    private DateTimeOffset? _lastModified;
    private bool _isDiscovered;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public bool RefreshOnStartup
    {
        get => _refreshOnStartup;
        set => SetProperty(ref _refreshOnStartup, value);
    }

    public int Priority
    {
        get => _priority;
        set => SetProperty(ref _priority, value);
    }

    public DateTimeOffset? LastRefreshAt
    {
        get => _lastRefreshAt;
        set
        {
            if (SetProperty(ref _lastRefreshAt, value))
            {
                OnPropertyChanged(nameof(LastRefreshDisplayText));
            }
        }
    }

    public string? ETag
    {
        get => _eTag;
        set => SetProperty(ref _eTag, value);
    }

    public DateTimeOffset? LastModified
    {
        get => _lastModified;
        set => SetProperty(ref _lastModified, value);
    }

    public bool IsDiscovered
    {
        get => _isDiscovered;
        set => SetProperty(ref _isDiscovered, value);
    }

    public string LastRefreshDisplayText => FormatLocalTimestamp(LastRefreshAt);

    public PlaylistSource Clone()
    {
        return new PlaylistSource
        {
            Id = Id,
            Name = Name,
            Url = Url,
            Enabled = Enabled,
            RefreshOnStartup = RefreshOnStartup,
            Priority = Priority,
            LastRefreshAt = LastRefreshAt,
            ETag = ETag,
            LastModified = LastModified,
            IsDiscovered = IsDiscovered
        };
    }

    private static string FormatLocalTimestamp(DateTimeOffset? value)
    {
        if (!value.HasValue)
        {
            return string.Empty;
        }

        return value.Value.ToLocalTime().DateTime.ToString("g", CultureInfo.CurrentCulture);
    }
}
