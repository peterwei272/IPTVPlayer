using System.IO;

namespace IPTVPlayer.App.Services;

public sealed class StoragePaths
{
    private StoragePaths(string dataRoot, bool isPortable)
    {
        DataRoot = dataRoot;
        IsPortable = isPortable;
        CacheRoot = Path.Combine(DataRoot, "cache");
        PlaylistCacheRoot = Path.Combine(CacheRoot, "playlists");
        EpgCacheRoot = Path.Combine(CacheRoot, "epg");
        SettingsFilePath = Path.Combine(DataRoot, "settings.json");
        MpvDirectory = Path.Combine(AppContext.BaseDirectory, "mpv");
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(CacheRoot);
        Directory.CreateDirectory(PlaylistCacheRoot);
        Directory.CreateDirectory(EpgCacheRoot);
    }

    public string DataRoot { get; }

    public string CacheRoot { get; }

    public string PlaylistCacheRoot { get; }

    public string EpgCacheRoot { get; }

    public string SettingsFilePath { get; }

    public string MpvDirectory { get; }

    public bool IsPortable { get; }

    public static StoragePaths Create()
    {
        var installedModeMarker = Path.Combine(AppContext.BaseDirectory, "installed.mode");
        if (File.Exists(installedModeMarker))
        {
            var installedLocalRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IPTVPlayer");
            Directory.CreateDirectory(installedLocalRoot);
            return new StoragePaths(installedLocalRoot, isPortable: false);
        }

        var portableRoot = Path.Combine(AppContext.BaseDirectory, "data");
        if (CanWriteToDirectory(portableRoot))
        {
            return new StoragePaths(portableRoot, isPortable: true);
        }

        var localRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IPTVPlayer");
        Directory.CreateDirectory(localRoot);
        return new StoragePaths(localRoot, isPortable: false);
    }

    public string GetPlaylistCacheFile(Guid sourceId)
    {
        return Path.Combine(PlaylistCacheRoot, $"{sourceId:N}.json");
    }

    public string GetEpgCacheFile(Guid sourceId)
    {
        return Path.Combine(EpgCacheRoot, $"{sourceId:N}.json.gz");
    }

    public string GetStorageModeLabel()
    {
        return IsPortable ? $"绿色模式: {DataRoot}" : $"本地数据: {DataRoot}";
    }

    private static bool CanWriteToDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probeFile = Path.Combine(directory, ".probe");
            File.WriteAllText(probeFile, "ok");
            File.Delete(probeFile);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
