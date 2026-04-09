using System.Globalization;
using System.IO.Compression;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;
using IPTVPlayer.App.Models;

namespace IPTVPlayer.App.Services;

public sealed class XmlTvParser
{
    public EpgCacheRecord Parse(EpgSource source, byte[] payload)
    {
        using var rawStream = new MemoryStream(payload, writable: false);
        using var stream = OpenStream(source, rawStream);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreComments = true,
            IgnoreWhitespace = true
        });

        var channels = new List<EpgChannel>();
        var programmes = new List<ProgrammeItem>();

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (reader.Name.Equals("channel", StringComparison.OrdinalIgnoreCase))
            {
                channels.Add(ReadChannel(reader));
                continue;
            }

            if (reader.Name.Equals("programme", StringComparison.OrdinalIgnoreCase))
            {
                var programme = ReadProgramme(source.Id, reader);
                if (programme is not null)
                {
                    programmes.Add(programme);
                }
            }
        }

        return new EpgCacheRecord
        {
            SourceId = source.Id,
            RefreshedAt = DateTimeOffset.UtcNow,
            Channels = channels,
            Programmes = programmes
        };
    }

    private static Stream OpenStream(EpgSource source, Stream rawStream)
    {
        var isCompressed = source.FormatHint.Equals("XmlTvGZip", StringComparison.OrdinalIgnoreCase) ||
                           LooksLikeGZip(rawStream);
        rawStream.Position = 0;
        return isCompressed ? new GZipStream(rawStream, CompressionMode.Decompress) : rawStream;
    }

    private static bool LooksLikeGZip(Stream stream)
    {
        Span<byte> magicBytes = stackalloc byte[2];
        var bytesRead = stream.Read(magicBytes);
        return bytesRead == 2 && magicBytes[0] == 0x1F && magicBytes[1] == 0x8B;
    }

    private static EpgChannel ReadChannel(XmlReader reader)
    {
        var channel = new EpgChannel
        {
            Id = reader.GetAttribute("id") ?? string.Empty
        };

        if (reader.IsEmptyElement)
        {
            return channel;
        }

        using var subtree = reader.ReadSubtree();
        while (subtree.Read())
        {
            if (subtree.NodeType == XmlNodeType.Element &&
                subtree.Name.Equals("display-name", StringComparison.OrdinalIgnoreCase))
            {
                var value = subtree.ReadElementContentAsString().Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    channel.DisplayNames.Add(value);
                }
            }
        }

        return channel;
    }

    private static ProgrammeItem? ReadProgramme(Guid sourceId, XmlReader reader)
    {
        var channelId = reader.GetAttribute("channel") ?? string.Empty;
        var startText = reader.GetAttribute("start");
        var stopText = reader.GetAttribute("stop");
        if (string.IsNullOrWhiteSpace(channelId) ||
            !TryParseXmlTvTime(startText, out var startUtc) ||
            !TryParseXmlTvTime(stopText, out var stopUtc))
        {
            return null;
        }

        var programme = new ProgrammeItem
        {
            EpgSourceId = sourceId,
            ChannelId = channelId,
            StartUtc = startUtc,
            EndUtc = stopUtc
        };

        if (reader.IsEmptyElement)
        {
            return programme;
        }

        using var subtree = reader.ReadSubtree();
        while (subtree.Read())
        {
            if (subtree.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            switch (subtree.Name)
            {
                case "title":
                    programme.Title = subtree.ReadElementContentAsString().Trim();
                    break;
                case "sub-title":
                    programme.Subtitle = subtree.ReadElementContentAsString().Trim();
                    break;
                case "desc":
                    programme.Description = subtree.ReadElementContentAsString().Trim();
                    break;
                case "category":
                    programme.Category = subtree.ReadElementContentAsString().Trim();
                    break;
            }
        }

        return string.IsNullOrWhiteSpace(programme.Title) ? null : programme;
    }

    private static bool TryParseXmlTvTime(string? value, out DateTimeOffset result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = Regex.Replace(value.Trim(), "\\s+", " ");
        if (trimmed.Length > 5 && (trimmed[^5] == '+' || trimmed[^5] == '-'))
        {
            trimmed = $"{trimmed[..^5].TrimEnd()} {trimmed[^5..^2]}:{trimmed[^2..]}";
        }

        foreach (var format in new[] { "yyyyMMddHHmmss zzz", "yyyyMMddHHmm zzz", "yyyyMMddHHmmss", "yyyyMMddHHmm" })
        {
            if (DateTimeOffset.TryParseExact(
                    trimmed,
                    format,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var parsed))
            {
                result = parsed.ToUniversalTime();
                return true;
            }
        }

        return false;
    }
}
