using System.IO;
using System.Net.Http;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using IPTVPlayer.App.Models;

namespace IPTVPlayer.App.Services;

public sealed class RefreshService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly StoragePaths _paths;
    private readonly SettingsStore _settingsStore;
    private readonly M3uPlaylistParser _playlistParser;
    private readonly XmlTvParser _xmlTvParser;
    private readonly HttpClient _httpClient;

    public RefreshService(
        StoragePaths paths,
        SettingsStore settingsStore,
        M3uPlaylistParser playlistParser,
        XmlTvParser xmlTvParser)
    {
        _paths = paths;
        _settingsStore = settingsStore;
        _playlistParser = playlistParser;
        _xmlTvParser = xmlTvParser;
        _httpClient = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    public async Task<AppDataSnapshot> LoadSnapshotAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var playlistSources = settings.PlaylistSources
            .Where(source => source.Enabled && !string.IsNullOrWhiteSpace(source.Url))
            .OrderBy(source => source.Priority)
            .ToList();
        var epgSources = settings.EpgSources
            .Where(source => source.Enabled && !string.IsNullOrWhiteSpace(source.Url))
            .OrderBy(source => source.Priority)
            .ToList();

        var playlistCaches = new List<(PlaylistSource Source, PlaylistCacheRecord Cache)>();
        foreach (var source in playlistSources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cache = await LoadPlaylistCacheAsync(source.Id).ConfigureAwait(false);
            if (cache is not null)
            {
                playlistCaches.Add((source, cache));
            }
        }

        var epgCaches = new Dictionary<Guid, EpgCacheRecord>();
        foreach (var source in epgSources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cache = await LoadEpgCacheAsync(source.Id).ConfigureAwait(false);
            if (cache is not null)
            {
                epgCaches[source.Id] = cache;
            }
        }

        return BuildSnapshot(playlistCaches, epgSources, epgCaches, settings);
    }

    public async Task<RefreshResult> RefreshAsync(
        AppSettings settings,
        bool startupRefresh,
        CancellationToken cancellationToken)
    {
        var statusMessages = new List<string>();

        foreach (var source in settings.PlaylistSources
                     .Where(source => source.Enabled &&
                                      !string.IsNullOrWhiteSpace(source.Url) &&
                                      (!startupRefresh || source.RefreshOnStartup))
                     .OrderBy(source => source.Priority))
        {
            cancellationToken.ThrowIfCancellationRequested();
            statusMessages.Add(await RefreshPlaylistSourceAsync(source, settings, cancellationToken).ConfigureAwait(false));
        }

        foreach (var source in settings.EpgSources
                     .Where(source => source.Enabled &&
                                      !string.IsNullOrWhiteSpace(source.Url) &&
                                      (!startupRefresh || source.RefreshOnStartup))
                     .OrderBy(source => source.Priority))
        {
            cancellationToken.ThrowIfCancellationRequested();
            statusMessages.Add(await RefreshEpgSourceAsync(source, cancellationToken).ConfigureAwait(false));
        }

        await _settingsStore.SaveAsync(settings).ConfigureAwait(false);
        var snapshot = await LoadSnapshotAsync(settings, cancellationToken).ConfigureAwait(false);
        return new RefreshResult(
            snapshot,
            string.Join(" | ", statusMessages.Where(message => !string.IsNullOrWhiteSpace(message))));
    }

    private async Task<string> RefreshPlaylistSourceAsync(
        PlaylistSource source,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, source.Url);
            ApplyConditionalHeaders(request.Headers, source.ETag, source.LastModified);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                source.LastRefreshAt = DateTimeOffset.Now;
                return $"{source.Name}: 已使用缓存";
            }

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var cache = _playlistParser.Parse(source, body);
            await SavePlaylistCacheAsync(source.Id, cache).ConfigureAwait(false);
            source.LastRefreshAt = DateTimeOffset.Now;
            source.ETag = response.Headers.ETag?.Tag;
            source.LastModified = response.Content.Headers.LastModified;

            foreach (var discoveredUrl in cache.SuggestedEpgUrls)
            {
                if (settings.EpgSources.Any(epg => epg.Url.Equals(discoveredUrl, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                settings.EpgSources.Add(new EpgSource
                {
                    Name = $"自动发现 EPG {settings.EpgSources.Count + 1}",
                    Url = discoveredUrl,
                    Enabled = true,
                    RefreshOnStartup = true,
                    Priority = settings.EpgSources.Count,
                    FormatHint = SettingsStore.InferEpgFormat(discoveredUrl),
                    IsDiscovered = true
                });
            }

            _settingsStore.Normalize(settings);
            return $"{source.Name}: 频道 {cache.Channels.Count} 个";
        }
        catch (Exception ex)
        {
            return $"{source.Name}: 刷新失败 ({ex.Message})";
        }
    }

    private async Task<string> RefreshEpgSourceAsync(EpgSource source, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, source.Url);
            ApplyConditionalHeaders(request.Headers, source.ETag, source.LastModified);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                source.LastRefreshAt = DateTimeOffset.Now;
                return $"{source.Name}: 已使用缓存";
            }

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var cache = _xmlTvParser.Parse(source, payload);
            await SaveEpgCacheAsync(source.Id, cache).ConfigureAwait(false);
            source.LastRefreshAt = DateTimeOffset.Now;
            source.ETag = response.Headers.ETag?.Tag;
            source.LastModified = response.Content.Headers.LastModified;
            return $"{source.Name}: 节目 {cache.Programmes.Count} 条";
        }
        catch (Exception ex)
        {
            return $"{source.Name}: 刷新失败 ({ex.Message})";
        }
    }

    private AppDataSnapshot BuildSnapshot(
        List<(PlaylistSource Source, PlaylistCacheRecord Cache)> playlistCaches,
        List<EpgSource> epgSources,
        Dictionary<Guid, EpgCacheRecord> epgCaches,
        AppSettings settings)
    {
        var programmeLookup = BuildProgrammeLookup(epgCaches);
        var lookupEntries = epgSources
            .Where(source => epgCaches.ContainsKey(source.Id))
            .Select(source => (SourceId: source.Id, Lookup: BuildLookup(epgCaches[source.Id])))
            .ToList();

        var channels = new List<ChannelItem>();
        foreach (var (_, cache) in playlistCaches)
        {
            var usesLegacyOrder = cache.Channels.Count > 1 && cache.Channels.All(channel => channel.PlaylistOrder == 0);
            foreach (var channel in cache.Channels
                         .Select((item, index) => (Channel: item, Index: index))
                         .OrderBy(entry => usesLegacyOrder ? entry.Index : entry.Channel.PlaylistOrder))
            {
                var current = channel.Channel;
                if (usesLegacyOrder)
                {
                    current.PlaylistOrder = channel.Index;
                }

                current.MatchedEpgSourceId = null;
                current.MatchedEpgChannelId = null;
                current.MatchResult = null;

                foreach (var (sourceId, lookup) in lookupEntries)
                {
                    if (!TryMatchChannel(current, lookup, out var match))
                    {
                        continue;
                    }

                    if (lookup.ChannelsWithProgrammes.Contains(match.EpgChannelId))
                    {
                        current.MatchedEpgSourceId = sourceId;
                        current.MatchedEpgChannelId = match.EpgChannelId;
                        current.MatchResult = match;
                        break;
                    }

                    current.MatchResult ??= match;
                }

                channels.Add(current);
            }
        }

        return new AppDataSnapshot
        {
            Channels = channels,
            EpgCaches = epgCaches,
            ProgrammeLookup = programmeLookup
        };
    }

    private static void ApplyConditionalHeaders(
        HttpRequestHeaders headers,
        string? eTag,
        DateTimeOffset? lastModified)
    {
        if (!string.IsNullOrWhiteSpace(eTag))
        {
            headers.IfNoneMatch.Add(new EntityTagHeaderValue(eTag));
        }

        if (lastModified.HasValue)
        {
            headers.IfModifiedSince = lastModified;
        }
    }

    private static Dictionary<string, List<ProgrammeItem>> BuildProgrammeLookup(Dictionary<Guid, EpgCacheRecord> epgCaches)
    {
        var programmeLookup = new Dictionary<string, List<ProgrammeItem>>(StringComparer.Ordinal);
        foreach (var cache in epgCaches.Values)
        {
            foreach (var grouping in cache.Programmes.GroupBy(
                         programme => AppDataSnapshot.ComposeProgrammeKey(programme.EpgSourceId, programme.ChannelId)))
            {
                programmeLookup[grouping.Key] = grouping
                    .OrderBy(programme => programme.StartUtc)
                    .ToList();
            }
        }

        return programmeLookup;
    }

    private static EpgLookup BuildLookup(EpgCacheRecord cache)
    {
        var byId = new Dictionary<string, string>(StringComparer.Ordinal);
        var byDisplayName = new Dictionary<string, string>(StringComparer.Ordinal);
        var channelsWithProgrammes = cache.Programmes
            .Select(programme => programme.ChannelId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var channel in cache.Channels)
        {
            if (!string.IsNullOrWhiteSpace(channel.Id) && !byId.ContainsKey(channel.Id))
            {
                byId[channel.Id] = channel.Id;
            }

            foreach (var displayName in channel.DisplayNames.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                if (!byDisplayName.ContainsKey(displayName))
                {
                    byDisplayName[displayName] = channel.Id;
                }
            }
        }

        return new EpgLookup(byId, byDisplayName, channelsWithProgrammes);
    }

    private static bool TryMatchChannel(ChannelItem channel, EpgLookup lookup, out ChannelMatchResult match)
    {
        if (!string.IsNullOrWhiteSpace(channel.TvgId) && lookup.ById.TryGetValue(channel.TvgId, out var byTvgId))
        {
            match = new ChannelMatchResult
            {
                Strategy = "tvg-id",
                SourceValue = channel.TvgId,
                EpgChannelId = byTvgId
            };
            return true;
        }

        if (!string.IsNullOrWhiteSpace(channel.TvgName))
        {
            if (lookup.ById.TryGetValue(channel.TvgName, out var byNameId))
            {
                match = new ChannelMatchResult
                {
                    Strategy = "tvg-name->channel-id",
                    SourceValue = channel.TvgName,
                    EpgChannelId = byNameId
                };
                return true;
            }

            if (lookup.ByDisplayName.TryGetValue(channel.TvgName, out var byNameDisplay))
            {
                match = new ChannelMatchResult
                {
                    Strategy = "tvg-name->display-name",
                    SourceValue = channel.TvgName,
                    EpgChannelId = byNameDisplay
                };
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(channel.DisplayName))
        {
            if (lookup.ById.TryGetValue(channel.DisplayName, out var byDisplayId))
            {
                match = new ChannelMatchResult
                {
                    Strategy = "display-name->channel-id",
                    SourceValue = channel.DisplayName,
                    EpgChannelId = byDisplayId
                };
                return true;
            }

            if (lookup.ByDisplayName.TryGetValue(channel.DisplayName, out var byDisplayName))
            {
                match = new ChannelMatchResult
                {
                    Strategy = "display-name->display-name",
                    SourceValue = channel.DisplayName,
                    EpgChannelId = byDisplayName
                };
                return true;
            }
        }

        match = new ChannelMatchResult
        {
            Strategy = "none",
            SourceValue = string.Empty,
            EpgChannelId = string.Empty
        };
        return false;
    }

    private async Task<PlaylistCacheRecord?> LoadPlaylistCacheAsync(Guid sourceId)
    {
        var path = _paths.GetPlaylistCacheFile(sourceId);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<PlaylistCacheRecord>(stream, JsonOptions).ConfigureAwait(false);
    }

    private async Task<EpgCacheRecord?> LoadEpgCacheAsync(Guid sourceId)
    {
        var path = _paths.GetEpgCacheFile(sourceId);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        await using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        return await JsonSerializer.DeserializeAsync<EpgCacheRecord>(gzip, JsonOptions).ConfigureAwait(false);
    }

    private async Task SavePlaylistCacheAsync(Guid sourceId, PlaylistCacheRecord cache)
    {
        var path = _paths.GetPlaylistCacheFile(sourceId);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, cache, JsonOptions).ConfigureAwait(false);
    }

    private async Task SaveEpgCacheAsync(Guid sourceId, EpgCacheRecord cache)
    {
        var path = _paths.GetEpgCacheFile(sourceId);
        await using var stream = File.Create(path);
        await using var gzip = new GZipStream(stream, CompressionLevel.Fastest);
        await JsonSerializer.SerializeAsync(gzip, cache, JsonOptions).ConfigureAwait(false);
    }

    private sealed record EpgLookup(
        Dictionary<string, string> ById,
        Dictionary<string, string> ByDisplayName,
        HashSet<string> ChannelsWithProgrammes);
}
