using System.Collections.ObjectModel;
using System.Windows;
using IPTVPlayer.App.Models;
using IPTVPlayer.App.Services;
using IPTVPlayer.App.ViewModels;

namespace IPTVPlayer.App;

public partial class SourceManagerWindow : Window
{
    public SourceManagerWindow(MainWindowViewModel.SourceEditorState state)
    {
        InitializeComponent();
        PlaylistSources = new ObservableCollection<PlaylistSource>(state.PlaylistSources);
        EpgSources = new ObservableCollection<EpgSource>(state.EpgSources);
        DataContext = this;
    }

    public ObservableCollection<PlaylistSource> PlaylistSources { get; }

    public ObservableCollection<EpgSource> EpgSources { get; }

    public MainWindowViewModel.SourceEditorState ResultState { get; private set; } =
        new(new List<PlaylistSource>(), new List<EpgSource>());

    private void AddPlaylist_Click(object sender, RoutedEventArgs e)
    {
        PlaylistSources.Add(new PlaylistSource
        {
            Name = "新订阅源",
            Enabled = true,
            RefreshOnStartup = true,
            Priority = PlaylistSources.Count
        });
    }

    private void RemovePlaylist_Click(object sender, RoutedEventArgs e)
    {
        if (PlaylistGrid.SelectedItem is PlaylistSource source)
        {
            PlaylistSources.Remove(source);
        }
    }

    private void MovePlaylistUp_Click(object sender, RoutedEventArgs e)
    {
        MoveSelectedItemUp(PlaylistGrid.SelectedIndex, PlaylistSources, index => PlaylistGrid.SelectedIndex = index);
    }

    private void MovePlaylistDown_Click(object sender, RoutedEventArgs e)
    {
        MoveSelectedItemDown(PlaylistGrid.SelectedIndex, PlaylistSources, index => PlaylistGrid.SelectedIndex = index);
    }

    private void AddEpg_Click(object sender, RoutedEventArgs e)
    {
        EpgSources.Add(new EpgSource
        {
            Name = "新 EPG 源",
            Enabled = true,
            RefreshOnStartup = true,
            Priority = EpgSources.Count,
            FormatHint = "Auto"
        });
    }

    private void RemoveEpg_Click(object sender, RoutedEventArgs e)
    {
        if (EpgGrid.SelectedItem is EpgSource source)
        {
            EpgSources.Remove(source);
        }
    }

    private void MoveEpgUp_Click(object sender, RoutedEventArgs e)
    {
        MoveSelectedItemUp(EpgGrid.SelectedIndex, EpgSources, index => EpgGrid.SelectedIndex = index);
    }

    private void MoveEpgDown_Click(object sender, RoutedEventArgs e)
    {
        MoveSelectedItemDown(EpgGrid.SelectedIndex, EpgSources, index => EpgGrid.SelectedIndex = index);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var playlists = PlaylistSources
            .Where(source => !string.IsNullOrWhiteSpace(source.Url))
            .Select((source, index) =>
            {
                source.Priority = index;
                return source;
            })
            .ToList();

        var epgs = EpgSources
            .Where(source => !string.IsNullOrWhiteSpace(source.Url))
            .Select((source, index) =>
            {
                source.Priority = index;
                source.FormatHint = SettingsStore.InferEpgFormat(source.Url);
                return source;
            })
            .ToList();

        ResultState = new MainWindowViewModel.SourceEditorState(playlists, epgs);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static void MoveSelectedItemUp<T>(int index, ObservableCollection<T> items, Action<int> setSelectedIndex)
    {
        if (index <= 0)
        {
            return;
        }

        items.Move(index, index - 1);
        setSelectedIndex(index - 1);
    }

    private static void MoveSelectedItemDown<T>(int index, ObservableCollection<T> items, Action<int> setSelectedIndex)
    {
        if (index < 0 || index >= items.Count - 1)
        {
            return;
        }

        items.Move(index, index + 1);
        setSelectedIndex(index + 1);
    }
}
