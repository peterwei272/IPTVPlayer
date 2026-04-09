using System.Collections.ObjectModel;
using System.Windows.Threading;
using IPTVPlayer.App.Infrastructure;
using IPTVPlayer.App.Models;
using IPTVPlayer.App.Services;
using IPTVPlayer.App.Services.Media;

namespace IPTVPlayer.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly StoragePaths _storagePaths;
    private readonly SettingsStore _settingsStore;
    private readonly RefreshService _refreshService;
    private readonly MpvPlayerService _playerService;
    private readonly DispatcherTimer _ticker;
    private readonly CancellationTokenSource _shutdownCts = new();
    private AppSettings _settings = new();
    private AppDataSnapshot _snapshot = new();
    private List<ChannelItem> _allChannels = new();
    private ChannelItem? _selectedChannel;
    private ChannelItem? _currentChannel;
    private string _searchText = string.Empty;
    private string _selectedGroup = "全部分组";
    private string _statusText = "准备就绪";
    private string _videoBadgeText = "未播放";
    private string _videoDetailText = "等待选择频道";
    private string _videoDynamicRange = "未播放";
    private string _videoResolutionText = string.Empty;
    private string _videoFrameRateText = string.Empty;
    private string _videoAudioChannelsText = string.Empty;
    private string _videoAudioCodecText = string.Empty;
    private string _videoAudioSampleRateText = string.Empty;
    private string _videoColorPrimariesText = string.Empty;
    private string _videoTechnicalSummary = "等待选择频道";
    private string _currentProgrammeTitle = "暂无节目";
    private string _currentProgrammeTime = string.Empty;
    private string _currentProgrammeDescription = "导入订阅后即可查看节目单。";
    private bool _isRefreshing;
    private bool _isDisposed;
    private bool _isPlayerFullScreen;
    private bool _suppressSelectionPlayback;
    private bool _hasInitializedStartupSelection;
    private Task? _activeRefreshTask;

    public MainWindowViewModel(
        StoragePaths storagePaths,
        SettingsStore settingsStore,
        RefreshService refreshService,
        MpvPlayerService playerService)
    {
        _storagePaths = storagePaths;
        _settingsStore = settingsStore;
        _refreshService = refreshService;
        _playerService = playerService;
        _ticker = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _ticker.Tick += (_, _) => UpdateLiveState();

        PlaySelectedCommand = new RelayCommand(PlaySelectedChannel, () => SelectedChannel is not null);
        RefreshCommand = new AsyncRelayCommand(() => RefreshAllAsync(startupRefresh: false), () => !IsRefreshing);
    }

    public ObservableCollection<ChannelItem> Channels { get; } = new();

    public ObservableCollection<string> Groups { get; } = new();

    public ObservableCollection<ProgrammeItem> UpcomingProgrammes { get; } = new();

    public ObservableCollection<string> VideoInfoBadges { get; } = new();

    public RelayCommand PlaySelectedCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public string StoragePathLabel => _storagePaths.GetStorageModeLabel();

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set
        {
            if (SetProperty(ref _isRefreshing, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RefreshChannelView();
            }
        }
    }

    public string SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (SetProperty(ref _selectedGroup, value))
            {
                RefreshChannelView();
            }
        }
    }

    public ChannelItem? SelectedChannel
    {
        get => _selectedChannel;
        set
        {
            if (SetProperty(ref _selectedChannel, value))
            {
                if (value is not null)
                {
                    _settings.LastSelectedChannelKey = value.ChannelKey;
                }

                PlaySelectedCommand.RaiseCanExecuteChanged();
                UpdateProgrammePanel();
            }
        }
    }

    public ChannelItem? CurrentChannel
    {
        get => _currentChannel;
        private set
        {
            if (SetProperty(ref _currentChannel, value))
            {
                OnPropertyChanged(nameof(CurrentChannelName));
                UpdateProgrammePanel();
            }
        }
    }

    public string CurrentChannelName => CurrentChannel?.DisplayName ?? "未播放";

    public bool IsPlayerFullScreen
    {
        get => _isPlayerFullScreen;
        set => SetProperty(ref _isPlayerFullScreen, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string VideoBadgeText
    {
        get => _videoBadgeText;
        private set => SetProperty(ref _videoBadgeText, value);
    }

    public string VideoDetailText
    {
        get => _videoDetailText;
        private set => SetProperty(ref _videoDetailText, value);
    }

    public string VideoDynamicRange
    {
        get => _videoDynamicRange;
        private set => SetProperty(ref _videoDynamicRange, value);
    }

    public string VideoResolutionText
    {
        get => _videoResolutionText;
        private set => SetProperty(ref _videoResolutionText, value);
    }

    public string VideoFrameRateText
    {
        get => _videoFrameRateText;
        private set => SetProperty(ref _videoFrameRateText, value);
    }

    public string VideoAudioChannelsText
    {
        get => _videoAudioChannelsText;
        private set => SetProperty(ref _videoAudioChannelsText, value);
    }

    public string VideoAudioCodecText
    {
        get => _videoAudioCodecText;
        private set => SetProperty(ref _videoAudioCodecText, value);
    }

    public string VideoAudioSampleRateText
    {
        get => _videoAudioSampleRateText;
        private set => SetProperty(ref _videoAudioSampleRateText, value);
    }

    public string VideoColorPrimariesText
    {
        get => _videoColorPrimariesText;
        private set => SetProperty(ref _videoColorPrimariesText, value);
    }

    public string VideoTechnicalSummary
    {
        get => _videoTechnicalSummary;
        private set => SetProperty(ref _videoTechnicalSummary, value);
    }

    public string CurrentProgrammeTitle
    {
        get => _currentProgrammeTitle;
        private set => SetProperty(ref _currentProgrammeTitle, value);
    }

    public string CurrentProgrammeTime
    {
        get => _currentProgrammeTime;
        private set => SetProperty(ref _currentProgrammeTime, value);
    }

    public string CurrentProgrammeDescription
    {
        get => _currentProgrammeDescription;
        private set => SetProperty(ref _currentProgrammeDescription, value);
    }

    public async Task InitializeAsync()
    {
        _settings = await _settingsStore.LoadAsync().ConfigureAwait(true);
        _snapshot = await _refreshService.LoadSnapshotAsync(_settings, _shutdownCts.Token).ConfigureAwait(true);
        ApplySnapshot(_snapshot, "已加载本地缓存");
        _ticker.Start();
        _activeRefreshTask = RefreshAllAsync(startupRefresh: true);
    }

    public void AttachPlayerHost(IntPtr hostHandle)
    {
        _playerService.AttachHost(hostHandle);
        UpdateVideoSignal();
        if (CurrentChannel is not null)
        {
            PlayChannel(CurrentChannel, userInitiated: false);
        }
    }

    public Task RefreshAllAsync(bool startupRefresh)
    {
        if (_isDisposed || _shutdownCts.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        if (_activeRefreshTask is not null && !_activeRefreshTask.IsCompleted)
        {
            return _activeRefreshTask;
        }

        _activeRefreshTask = RefreshAllCoreAsync(startupRefresh, _shutdownCts.Token);
        return _activeRefreshTask;
    }

    private async Task RefreshAllCoreAsync(bool startupRefresh, CancellationToken cancellationToken)
    {
        IsRefreshing = true;
        StatusText = startupRefresh ? "启动后正在后台更新订阅和节目单..." : "正在刷新订阅和节目单...";
        try
        {
            var result = await _refreshService.RefreshAsync(_settings, startupRefresh, cancellationToken).ConfigureAwait(true);
            ApplySnapshot(result.Snapshot, result.StatusMessage);
            if (!cancellationToken.IsCancellationRequested)
            {
                await _settingsStore.SaveAsync(_settings).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            if (!_shutdownCts.IsCancellationRequested)
            {
                StatusText = "刷新已取消";
            }
        }
        finally
        {
            IsRefreshing = false;
            _activeRefreshTask = null;
        }
    }

    public void PlaySelectedChannel()
    {
        if (SelectedChannel is not null)
        {
            PlayChannel(SelectedChannel, userInitiated: true);
        }
    }

    public void PlaySelectedChannelFromUserSelection()
    {
        if (_suppressSelectionPlayback || SelectedChannel is null)
        {
            return;
        }

        if (CurrentChannel?.ChannelKey == SelectedChannel.ChannelKey)
        {
            return;
        }

        PlayChannel(SelectedChannel, userInitiated: true);
    }

    public void PlayOffsetChannel(int offset)
    {
        if (Channels.Count == 0)
        {
            return;
        }

        var currentIndex = SelectedChannel is null ? -1 : Channels.IndexOf(SelectedChannel);
        if (currentIndex < 0 && CurrentChannel is not null)
        {
            currentIndex = Channels.IndexOf(CurrentChannel);
        }

        var nextIndex = currentIndex < 0
            ? (offset < 0 ? Channels.Count - 1 : 0)
            : (currentIndex + offset + Channels.Count) % Channels.Count;
        SelectedChannel = Channels[nextIndex];
        PlaySelectedChannel();
    }

    public SourceEditorState CreateSourceEditorState()
    {
        return new SourceEditorState(
            _settings.PlaylistSources.Select(source => source.Clone()).ToList(),
            _settings.EpgSources.Select(source => source.Clone()).ToList());
    }

    public void ApplySourceChanges(List<PlaylistSource> playlistSources, List<EpgSource> epgSources)
    {
        _settings.PlaylistSources = playlistSources;
        _settings.EpgSources = epgSources;
        _settingsStore.Normalize(_settings);
    }

    public Task PersistSettingsAsync()
    {
        return _settingsStore.SaveAsync(_settings);
    }

    public async Task ShutdownAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _ticker.Stop();
        _shutdownCts.Cancel();

        try
        {
            if (_activeRefreshTask is not null)
            {
                await _activeRefreshTask.ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
        }

        try
        {
            await _settingsStore.SaveAsync(_settings).ConfigureAwait(true);
        }
        catch
        {
        }

        Dispose();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _ticker.Stop();
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();
        _playerService.Dispose();
    }

    public sealed record SourceEditorState(
        List<PlaylistSource> PlaylistSources,
        List<EpgSource> EpgSources);

    private void ApplySnapshot(AppDataSnapshot snapshot, string statusMessage)
    {
        var selectedKey = SelectedChannel?.ChannelKey;
        var currentKey = CurrentChannel?.ChannelKey;

        _snapshot = snapshot;
        _allChannels = snapshot.Channels;
        UpdateGroups();
        using var _ = SuppressSelectionPlayback();
        RefreshChannelView();

        if (!_hasInitializedStartupSelection)
        {
            var initialChannel = Channels.FirstOrDefault() ?? _allChannels.FirstOrDefault();
            SelectedChannel = initialChannel;
            CurrentChannel = initialChannel;
            _hasInitializedStartupSelection = initialChannel is not null;
        }
        else
        {
            SelectedChannel = string.IsNullOrWhiteSpace(selectedKey)
                ? null
                : Channels.FirstOrDefault(channel => channel.ChannelKey == selectedKey);

            var nextCurrent = string.IsNullOrWhiteSpace(currentKey)
                ? null
                : _allChannels.FirstOrDefault(channel => channel.ChannelKey == currentKey);
            CurrentChannel = nextCurrent ?? _allChannels.FirstOrDefault();
        }

        UpdateVideoSignal();
        StatusText = string.IsNullOrWhiteSpace(statusMessage) ? "刷新完成" : statusMessage;

        if (CurrentChannel is not null && _playerService.IsAvailable)
        {
            PlayChannel(CurrentChannel, userInitiated: false);
        }
    }

    private void PlayChannel(ChannelItem channel, bool userInitiated)
    {
        if (!_playerService.Play(channel.StreamUrl, out var error))
        {
            StatusText = string.IsNullOrWhiteSpace(error) ? "播放失败" : error;
            UpdateVideoSignal();
            return;
        }

        CurrentChannel = channel;
        SelectedChannel = channel;
        _settings.LastPlayedChannelKey = channel.ChannelKey;
        UpdateVideoSignal();
        if (userInitiated)
        {
            StatusText = $"正在播放: {channel.DisplayName}";
        }
    }

    private void UpdateGroups()
    {
        var selected = SelectedGroup;
        Groups.Clear();
        Groups.Add("全部分组");

        var seen = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var group in _allChannels
                     .Select(channel => string.IsNullOrWhiteSpace(channel.GroupTitle) ? "未分组" : channel.GroupTitle))
        {
            if (seen.Add(group))
            {
                Groups.Add(group);
            }
        }

        SelectedGroup = Groups.Contains(selected) ? selected : "全部分组";
    }

    private void RefreshChannelView()
    {
        var selectedKey = SelectedChannel?.ChannelKey;
        var filtered = _allChannels.Where(channel =>
        {
            var groupMatches = SelectedGroup == "全部分组" || channel.GroupTitle == SelectedGroup;
            var searchMatches = string.IsNullOrWhiteSpace(SearchText) ||
                                channel.SearchText.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase);
            return groupMatches && searchMatches;
        }).ToList();

        using var _ = SuppressSelectionPlayback();
        Channels.Clear();
        foreach (var channel in filtered)
        {
            Channels.Add(channel);
        }

        SelectedChannel = string.IsNullOrWhiteSpace(selectedKey)
            ? null
            : Channels.FirstOrDefault(channel => channel.ChannelKey == selectedKey);
    }

    private void UpdateProgrammePanel()
    {
        UpcomingProgrammes.Clear();
        var programmeChannel = SelectedChannel ?? CurrentChannel;
        if (programmeChannel?.MatchedEpgSourceId is not Guid sourceId ||
            string.IsNullOrWhiteSpace(programmeChannel.MatchedEpgChannelId))
        {
            CurrentProgrammeTitle = "暂无节目单";
            CurrentProgrammeTime = string.Empty;
            CurrentProgrammeDescription = programmeChannel?.MatchResult is null
                ? "当前频道没有精确匹配到节目单。"
                : $"已精确匹配到频道，但所匹配的 EPG 源没有节目数据。匹配方式: {programmeChannel.MatchResult.Strategy}";
            return;
        }

        var key = AppDataSnapshot.ComposeProgrammeKey(sourceId, programmeChannel.MatchedEpgChannelId);
        if (!_snapshot.ProgrammeLookup.TryGetValue(key, out var programmes) || programmes.Count == 0)
        {
            CurrentProgrammeTitle = "暂无节目单";
            CurrentProgrammeTime = string.Empty;
            CurrentProgrammeDescription = "已精确匹配到频道，但当前节目单源没有节目数据。";
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var current = programmes.FirstOrDefault(programme => programme.IsCurrent(now));
        var upcoming = programmes.Where(programme => programme.EndUtc >= now).ToList();

        if (current is null)
        {
            CurrentProgrammeTitle = "当前时段暂无节目";
            CurrentProgrammeTime = string.Empty;
            CurrentProgrammeDescription = programmeChannel.MatchResult is null
                ? "等待节目单刷新。"
                : $"匹配方式: {programmeChannel.MatchResult.Strategy}";
        }
        else
        {
            CurrentProgrammeTitle = current.Title;
            CurrentProgrammeTime = ProgrammeItem.FormatScheduleRange(current.StartUtc, current.EndUtc);
            CurrentProgrammeDescription = string.IsNullOrWhiteSpace(current.Description)
                ? "当前节目暂无简介。"
                : current.Description!;
        }

        foreach (var programme in upcoming)
        {
            UpcomingProgrammes.Add(programme);
        }
    }

    private void UpdateLiveState()
    {
        UpdateVideoSignal();
        UpdateProgrammePanel();
    }

    private void UpdateVideoSignal()
    {
        var signal = _playerService.GetSignalInfo();
        VideoBadgeText = signal.BadgeText;
        VideoDetailText = signal.DetailText;
        VideoDynamicRange = signal.DynamicRange;
        VideoResolutionText = signal.Resolution ?? string.Empty;
        VideoFrameRateText = signal.FrameRate ?? string.Empty;
        VideoAudioChannelsText = signal.AudioChannels ?? string.Empty;
        VideoAudioCodecText = NormalizeAudioCodec(signal.AudioCodec);
        VideoAudioSampleRateText = signal.AudioSampleRate ?? string.Empty;
        VideoColorPrimariesText = NormalizeColorPrimaries(signal.ColorPrimaries);
        VideoTechnicalSummary = signal.TechnicalSummary ?? signal.DetailText;
        RebuildVideoInfoBadges();
    }

    private void RebuildVideoInfoBadges()
    {
        var values = new[]
        {
            VideoResolutionText,
            VideoFrameRateText,
            VideoAudioChannelsText,
            VideoAudioCodecText,
            VideoAudioSampleRateText,
            VideoColorPrimariesText
        }.Where(value => !string.IsNullOrWhiteSpace(value));

        VideoInfoBadges.Clear();
        foreach (var value in values)
        {
            VideoInfoBadges.Add(value);
        }
    }

    private static string NormalizeAudioCodec(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.ToUpperInvariant();
    }

    private static string NormalizeColorPrimaries(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Equals("bt.709", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return value;
    }

    private IDisposable SuppressSelectionPlayback()
    {
        return new SelectionPlaybackScope(this);
    }

    private sealed class SelectionPlaybackScope : IDisposable
    {
        private readonly MainWindowViewModel _owner;
        private readonly bool _previousValue;

        public SelectionPlaybackScope(MainWindowViewModel owner)
        {
            _owner = owner;
            _previousValue = owner._suppressSelectionPlayback;
            owner._suppressSelectionPlayback = true;
        }

        public void Dispose()
        {
            _owner._suppressSelectionPlayback = _previousValue;
        }
    }

}
