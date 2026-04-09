using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IPTVPlayer.App.Services;
using IPTVPlayer.App.Services.Media;
using IPTVPlayer.App.ViewModels;

namespace IPTVPlayer.App;

public partial class ShellWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private bool _allowClose;
    private bool _isFullScreen;
    private bool _isShuttingDown;
    private WindowStyle _restoreWindowStyle;
    private ResizeMode _restoreResizeMode;
    private WindowState _restoreWindowState;

    public ShellWindow()
    {
        InitializeComponent();

        var storagePaths = StoragePaths.Create();
        var settingsStore = new SettingsStore(storagePaths);
        var refreshService = new RefreshService(storagePaths, settingsStore, new M3uPlaylistParser(), new XmlTvParser());
        var playerService = new MpvPlayerService(storagePaths);
        _viewModel = new MainWindowViewModel(storagePaths, settingsStore, refreshService, playerService);
        DataContext = _viewModel;

        Loaded += ShellWindow_Loaded;
        Closing += ShellWindow_Closing;
        PlayerHost.HostHandleCreated += (_, handle) => _viewModel.AttachPlayerHost(handle);
    }

    private async void ShellWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async void ShellWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        if (_isShuttingDown)
        {
            return;
        }

        _isShuttingDown = true;
        try
        {
            await _viewModel.ShutdownAsync();
        }
        finally
        {
            _allowClose = true;
            Close();
        }
    }

    private async void OpenSourceManager_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SourceManagerWindow(_viewModel.CreateSourceEditorState())
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.ApplySourceChanges(dialog.ResultState.PlaylistSources, dialog.ResultState.EpgSources);
            await _viewModel.PersistSettingsAsync();
            await _viewModel.RefreshAllAsync(startupRefresh: false);
        }
    }

    private void ChannelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _viewModel.PlaySelectedChannelFromUserSelection();
    }

    private void PlayerSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleFullScreen();
        }
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                _viewModel.PlaySelectedChannel();
                e.Handled = true;
                break;
            case Key.Up:
                _viewModel.PlayOffsetChannel(-1);
                e.Handled = true;
                break;
            case Key.Down:
                _viewModel.PlayOffsetChannel(1);
                e.Handled = true;
                break;
            case Key.F5:
                await _viewModel.RefreshAllAsync(startupRefresh: false);
                e.Handled = true;
                break;
            case Key.F11:
                ToggleFullScreen();
                e.Handled = true;
                break;
            case Key.Escape when _isFullScreen:
                ToggleFullScreen();
                e.Handled = true;
                break;
        }
    }

    private void ToggleFullScreen()
    {
        if (_isFullScreen)
        {
            WindowStyle = _restoreWindowStyle;
            ResizeMode = _restoreResizeMode;
            WindowState = _restoreWindowState;
            ApplyPlayerFocusLayout(false);
            _viewModel.IsPlayerFullScreen = false;
            _isFullScreen = false;
            return;
        }

        _restoreWindowStyle = WindowStyle;
        _restoreResizeMode = ResizeMode;
        _restoreWindowState = WindowState;
        ApplyPlayerFocusLayout(true);
        _viewModel.IsPlayerFullScreen = true;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Maximized;
        _isFullScreen = true;
    }

    private void ApplyPlayerFocusLayout(bool isPlayerFocused)
    {
        RootLayout.Margin = isPlayerFocused ? new Thickness(0) : new Thickness(18);
        TopBar.Visibility = isPlayerFocused ? Visibility.Collapsed : Visibility.Visible;
        ChannelPane.Visibility = isPlayerFocused ? Visibility.Collapsed : Visibility.Visible;
        ProgrammePane.Visibility = isPlayerFocused ? Visibility.Collapsed : Visibility.Visible;
        PlayerHeader.Visibility = isPlayerFocused ? Visibility.Collapsed : Visibility.Visible;

        ChannelColumn.Width = isPlayerFocused ? new GridLength(0) : new GridLength(320);
        ProgrammeColumn.Width = isPlayerFocused ? new GridLength(0) : new GridLength(360);
        Grid.SetColumn(PlayerPane, isPlayerFocused ? 0 : 1);
        Grid.SetColumnSpan(PlayerPane, isPlayerFocused ? 3 : 1);
        PlayerPane.Margin = isPlayerFocused ? new Thickness(0) : new Thickness(0, 0, 14, 0);
        PlayerPane.Padding = isPlayerFocused ? new Thickness(0) : new Thickness(16);
        PlayerPane.CornerRadius = isPlayerFocused ? new CornerRadius(0) : new CornerRadius(18);
        PlayerPane.BorderThickness = isPlayerFocused ? new Thickness(0) : new Thickness(1);
        PlayerSurfaceBorder.CornerRadius = isPlayerFocused ? new CornerRadius(0) : new CornerRadius(18);
        PlayerSurfaceBorder.BorderThickness = isPlayerFocused ? new Thickness(0) : new Thickness(1);
    }
}
