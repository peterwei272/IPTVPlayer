using System.IO;
using System.Text.Json;
using IPTVPlayer.App.Models;

namespace IPTVPlayer.App.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly StoragePaths _paths;

    public SettingsStore(StoragePaths paths)
    {
        _paths = paths;
    }

    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(_paths.SettingsFilePath))
        {
            var defaults = CreateDefaultSettings();
            Normalize(defaults);
            await SaveAsync(defaults).ConfigureAwait(false);
            return defaults;
        }

        await using var stream = File.OpenRead(_paths.SettingsFilePath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions).ConfigureAwait(false)
            ?? CreateDefaultSettings();
        Normalize(settings);
        return settings;
    }

    public async Task SaveAsync(AppSettings settings)
    {
        Normalize(settings);
        await using var stream = File.Create(_paths.SettingsFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions).ConfigureAwait(false);
    }

    public void Normalize(AppSettings settings)
    {
        settings.PlaylistSources = settings.PlaylistSources
            .OrderBy(source => source.Priority)
            .ToList();
        settings.EpgSources = settings.EpgSources
            .OrderBy(source => source.Priority)
            .ToList();

        for (var index = 0; index < settings.PlaylistSources.Count; index++)
        {
            var source = settings.PlaylistSources[index];
            if (source.Id == Guid.Empty)
            {
                source.Id = Guid.NewGuid();
            }

            source.Priority = index;
            source.Name = source.Name?.Trim() ?? string.Empty;
            source.Url = source.Url?.Trim() ?? string.Empty;
        }

        for (var index = 0; index < settings.EpgSources.Count; index++)
        {
            var source = settings.EpgSources[index];
            if (source.Id == Guid.Empty)
            {
                source.Id = Guid.NewGuid();
            }

            source.Priority = index;
            source.Name = source.Name?.Trim() ?? string.Empty;
            source.Url = source.Url?.Trim() ?? string.Empty;
            source.FormatHint = string.IsNullOrWhiteSpace(source.FormatHint)
                ? InferEpgFormat(source.Url)
                : source.FormatHint;
        }

    }

    public static string InferEpgFormat(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "Auto";
        }

        if (url.EndsWith(".xml.gz", StringComparison.OrdinalIgnoreCase) ||
            url.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            return "XmlTvGZip";
        }

        if (url.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return "XmlTv";
        }

        return "Auto";
    }

    private static AppSettings CreateDefaultSettings()
    {
        return new AppSettings
        {
            PlaylistSources = new List<PlaylistSource>(),
            EpgSources = new List<EpgSource>()
        };
    }
}
