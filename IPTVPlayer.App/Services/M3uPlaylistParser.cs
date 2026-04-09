using System.IO;
using System.Text.RegularExpressions;
using IPTVPlayer.App.Models;

namespace IPTVPlayer.App.Services;

public sealed class M3uPlaylistParser
{
    private static readonly Regex AttributeRegex = new(
        "(?<key>[A-Za-z0-9-]+)=(?:\"(?<value>[^\"]*)\"|(?<value>[^\\s,]+))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public PlaylistCacheRecord Parse(PlaylistSource source, string content)
    {
        var channels = new List<ChannelItem>();
        var suggestedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        PendingChannel? pending = null;
        var playlistOrder = 0;

        using var reader = new StringReader(content);
        while (reader.ReadLine() is { } rawLine)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase))
            {
                var headerAttributes = ParseAttributes(line);
                if (headerAttributes.TryGetValue("url-tvg", out var urlTvg))
                {
                    foreach (var value in urlTvg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            suggestedUrls.Add(value);
                        }
                    }
                }

                continue;
            }

            if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                pending = ParseExtInf(source, line);
                continue;
            }

            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (pending is null)
            {
                continue;
            }

            channels.Add(new ChannelItem
            {
                ChannelKey = ComposeChannelKey(source.Id, line),
                SourceId = source.Id,
                SourceName = source.Name,
                PlaylistOrder = playlistOrder++,
                GroupTitle = pending.GroupTitle,
                DisplayName = pending.DisplayName,
                TvgName = pending.TvgName,
                TvgId = pending.TvgId,
                LogoUrl = pending.LogoUrl,
                StreamUrl = line,
                RawAttributes = pending.Attributes
            });

            pending = null;
        }

        return new PlaylistCacheRecord
        {
            SourceId = source.Id,
            RefreshedAt = DateTimeOffset.UtcNow,
            SuggestedEpgUrls = suggestedUrls.ToList(),
            Channels = channels
        };
    }

    public static string ComposeChannelKey(Guid sourceId, string streamUrl)
    {
        return $"{sourceId:N}|{streamUrl}";
    }

    private static PendingChannel ParseExtInf(PlaylistSource source, string line)
    {
        var body = line[(line.IndexOf(':') + 1)..];
        var separatorIndex = FindUnquotedComma(body);
        var attributesText = separatorIndex >= 0 ? body[..separatorIndex] : body;
        var displayName = separatorIndex >= 0 ? body[(separatorIndex + 1)..].Trim() : string.Empty;
        var attributes = ParseAttributes(attributesText);

        return new PendingChannel
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? attributes.GetValueOrDefault("tvg-name") ?? source.Name
                : displayName,
            GroupTitle = attributes.GetValueOrDefault("group-title") ?? "未分组",
            TvgName = attributes.GetValueOrDefault("tvg-name"),
            TvgId = attributes.GetValueOrDefault("tvg-id"),
            LogoUrl = attributes.GetValueOrDefault("tvg-logo"),
            Attributes = attributes
        };
    }

    private static Dictionary<string, string> ParseAttributes(string text)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributeRegex.Matches(text))
        {
            attributes[match.Groups["key"].Value] = match.Groups["value"].Value.Trim();
        }

        return attributes;
    }

    private static int FindUnquotedComma(string text)
    {
        var inQuotes = false;
        for (var index = 0; index < text.Length; index++)
        {
            switch (text[index])
            {
                case '"':
                    inQuotes = !inQuotes;
                    break;
                case ',' when !inQuotes:
                    return index;
            }
        }

        return -1;
    }

    private sealed class PendingChannel
    {
        public string DisplayName { get; set; } = string.Empty;

        public string GroupTitle { get; set; } = string.Empty;

        public string? TvgName { get; set; }

        public string? TvgId { get; set; }

        public string? LogoUrl { get; set; }

        public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
