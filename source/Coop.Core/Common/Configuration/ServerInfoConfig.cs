using Common.Logging;
using GameInterface.Services.UI.ServerInfo;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Coop.Core.Common.Configuration;

/// <summary>Operator-written details the server shares with joining players.</summary>
public interface IServerInfoConfig
{
    /// <summary>Paragraphs of the message of the day.</summary>
    IReadOnlyList<string> Motd { get; }

    /// <summary>Server rules in the operator's order.</summary>
    IReadOnlyList<string> Rules { get; }

    /// <summary>Links whose address passed <see cref="IServerInfoLinkRules"/>, in its normalized form.</summary>
    IReadOnlyList<ServerInfoLink> Links { get; }

    /// <summary>News entries in the operator's order.</summary>
    IReadOnlyList<ServerInfoNews> News { get; }
}

/// <summary>
/// Reads the optional server-info.json once per server container. A missing file gives no server info,
/// and an unreadable one is logged and ignored.
/// </summary>
internal sealed class ServerInfoConfig : IServerInfoConfig
{
    private const string FileName = "server-info.json";
    private const string FileEnvironmentVariable = "COOP_SERVER_INFO_FILE";

    // Far more than the 12000 characters the caps keep, even with every character escaped, so only a wrong file is refused.
    internal const long MaxFileBytes = 256 * 1024;
    // Later bad links are only counted, so a file full of broken links logs a few lines.
    internal const int MaxLoggedBadLinks = ServerInfoLimits.MaxLinks;

    private static readonly ILogger Logger = LogManager.GetLogger<ServerInfoConfig>();

    private readonly IServerInfoLinkRules linkRules;

    public ServerInfoConfig(IServerDataPath dataPath, IServerInfoLinkRules linkRules)
        : this(() => dataPath.Resolve(FileEnvironmentVariable, FileName), linkRules)
    {
    }

    internal ServerInfoConfig(string path, IServerInfoLinkRules linkRules) : this(() => path, linkRules)
    {
    }

    private ServerInfoConfig(Func<string> resolvePath, IServerInfoLinkRules linkRules)
    {
        if (resolvePath == null) throw new ArgumentNullException(nameof(resolvePath));
        if (linkRules == null) throw new ArgumentNullException(nameof(linkRules));

        this.linkRules = linkRules;
        Load(resolvePath);
    }

    public IReadOnlyList<string> Motd { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> Rules { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<ServerInfoLink> Links { get; private set; } = Array.Empty<ServerInfoLink>();
    public IReadOnlyList<ServerInfoNews> News { get; private set; } = Array.Empty<ServerInfoNews>();

    private void Load(Func<string> resolvePath)
    {
        string path = string.Empty;

        try
        {
            path = resolvePath();

            string json;
            try
            {
                json = ReadFile(path);
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }

            if (json == null)
            {
                Logger.Warning("Server info in {Path} is larger than {MaxBytes} bytes; running without server info", path, MaxFileBytes);
                return;
            }

            using JsonDocument document = JsonDocument.Parse(json);
            Read(document.RootElement, path);
            Logger.Information(
                "Server info loaded from {Path} (motd {Motd} paragraph(s), rules {Rules}, links {Links}, news {News})",
                path,
                Motd.Count,
                Rules.Count,
                Links.Count,
                News.Count);
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is JsonException ||
            // A lone surrogate escape in a key that a lookup has to unescape.
            exception is InvalidOperationException ||
            exception is ArgumentException ||
            exception is NotSupportedException)
        {
            Logger.Error(exception, "Server info could not be read from {Path}; running without server info", path);
            // A key lookup can throw after earlier keys were read, and a broken file shows nothing.
            Motd = Array.Empty<string>();
            Rules = Array.Empty<string>();
            Links = Array.Empty<ServerInfoLink>();
            News = Array.Empty<ServerInfoNews>();
        }
    }

    // Gives null for a file over MaxFileBytes without reading it, so a huge file cannot fill the server's memory.
    private static string ReadFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaxFileBytes) return null;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private void Read(JsonElement root, string path)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            Logger.Warning("Server info in {Path} is not a JSON object; running without server info", path);
            return;
        }

        Motd = ReadTexts(root, "motd", ServerInfoLimits.MaxMotdParagraphs, ServerInfoLimits.MaxMotdLength, path);
        Rules = ReadTexts(root, "rules", ServerInfoLimits.MaxRules, ServerInfoLimits.MaxRulesLength, path);
        Links = ReadLinks(root, path);

        int used = Motd.Sum(paragraph => paragraph.Length) +
            Rules.Sum(rule => rule.Length) +
            Links.Sum(link => link.Label.Length + link.Url.Length);
        News = ReadNews(root, ServerInfoLimits.MaxTotalLength - used, path);
    }

    private static string[] ReadTexts(JsonElement root, string key, int maxCount, int maxLength, string path)
    {
        if (!TryGetArray(root, key, path, out JsonElement array)) return Array.Empty<string>();

        var texts = new List<string>();
        int length = 0;
        int skipped = 0;
        bool trimmed = false;
        foreach (JsonElement entry in array.EnumerateArray())
        {
            if (!TryReadText(entry, out string raw))
            {
                skipped++;
                continue;
            }

            string text = RemoveControlCharacters(raw);
            if (text.Length == 0) continue;

            int room = maxLength - length;
            if (texts.Count == maxCount || room == 0)
            {
                trimmed = true;
                break;
            }

            if (text.Length > room)
            {
                // The rest of the key is dropped too, so no later entry gets a fragment.
                text = Truncate(text, room);
                if (text.Length > 0) texts.Add(text);
                trimmed = true;
                break;
            }

            texts.Add(text);
            length += text.Length;
        }

        if (skipped > 0)
        {
            Logger.Warning("Server info {Key} in {Path} has {Count} entry(s) that are not valid strings; skipping them", key, path, skipped);
        }

        if (trimmed)
        {
            Logger.Warning(
                "Server info {Key} in {Path} was cut to {MaxCount} entry(s) and {MaxLength} characters",
                key,
                path,
                maxCount,
                maxLength);
        }

        return texts.ToArray();
    }

    private ServerInfoLink[] ReadLinks(JsonElement root, string path)
    {
        if (!TryGetArray(root, "links", path, out JsonElement array)) return Array.Empty<ServerInfoLink>();

        var links = new List<ServerInfoLink>();
        bool trimmed = false;
        int index = -1;
        int dropped = 0;
        foreach (JsonElement entry in array.EnumerateArray())
        {
            index++;
            if (!TryReadLink(entry, ref trimmed, out ServerInfoLink link))
            {
                dropped++;
                if (dropped <= MaxLoggedBadLinks)
                {
                    Logger.Warning(
                        "Server info link at index {Index} in {Path} has no usable http or https url; skipping it",
                        index,
                        path);
                }

                continue;
            }

            if (links.Count == ServerInfoLimits.MaxLinks)
            {
                trimmed = true;
                break;
            }

            links.Add(link);
        }

        if (dropped > MaxLoggedBadLinks)
        {
            Logger.Warning(
                "Server info links in {Path} have {Count} more link(s) with no usable http or https url; skipping them",
                path,
                dropped - MaxLoggedBadLinks);
        }

        if (trimmed)
        {
            Logger.Warning(
                "Server info links in {Path} were cut to {MaxLinks} link(s) and {MaxLabelLength} label characters",
                path,
                ServerInfoLimits.MaxLinks,
                ServerInfoLimits.MaxLinkLabelLength);
        }

        return links.ToArray();
    }

    // A missing, empty or non-text label is kept empty; the client then shows the address.
    private bool TryReadLink(JsonElement entry, ref bool trimmed, out ServerInfoLink link)
    {
        link = null;
        if (entry.ValueKind != JsonValueKind.Object) return false;

        try
        {
            if (!entry.TryGetProperty("url", out JsonElement url) ||
                !TryReadText(url, out string address) ||
                !linkRules.TryNormalize(address, out string normalized)) return false;

            string label = ReadField(entry, "label", ServerInfoLimits.MaxLinkLabelLength, ref trimmed);
            link = new ServerInfoLink { Label = label, Url = normalized };
            return true;
        }
        catch (InvalidOperationException)
        {
            // A lone surrogate escape in one of this entry's keys.
            return false;
        }
    }

    private static ServerInfoNews[] ReadNews(JsonElement root, int room, string path)
    {
        if (!TryGetArray(root, "news", path, out JsonElement array)) return Array.Empty<ServerInfoNews>();

        var news = new List<ServerInfoNews>();
        int skipped = 0;
        bool trimmed = false;
        bool overTotal = false;
        foreach (JsonElement entry in array.EnumerateArray())
        {
            if (!TryReadNews(entry, ref trimmed, out ServerInfoNews item))
            {
                skipped++;
                continue;
            }

            if (news.Count == ServerInfoLimits.MaxNews)
            {
                trimmed = true;
                break;
            }

            int length = item.Date.Length + item.Title.Length + item.Text.Length;
            if (length > room)
            {
                overTotal = true;
                break;
            }

            room -= length;
            news.Add(item);
        }

        if (skipped > 0)
        {
            Logger.Warning("Server info news in {Path} has {Count} entry(s) that are not objects with a title or text; skipping them", path, skipped);
        }

        if (trimmed)
        {
            Logger.Warning(
                "Server info news in {Path} was cut to {MaxNews} entry(s) with {MaxDate} date, {MaxTitle} title and {MaxText} text characters",
                path,
                ServerInfoLimits.MaxNews,
                ServerInfoLimits.MaxNewsDateLength,
                ServerInfoLimits.MaxNewsTitleLength,
                ServerInfoLimits.MaxNewsTextLength);
        }

        if (overTotal)
        {
            Logger.Warning(
                "Server info news in {Path} was cut to keep the server info under {MaxTotalLength} characters",
                path,
                ServerInfoLimits.MaxTotalLength);
        }

        return news.ToArray();
    }

    // An entry needs a title or text; a missing or non-text field reads as empty.
    private static bool TryReadNews(JsonElement entry, ref bool trimmed, out ServerInfoNews item)
    {
        item = null;
        if (entry.ValueKind != JsonValueKind.Object) return false;

        try
        {
            string date = ReadField(entry, "date", ServerInfoLimits.MaxNewsDateLength, ref trimmed);
            string title = ReadField(entry, "title", ServerInfoLimits.MaxNewsTitleLength, ref trimmed);
            string text = ReadField(entry, "text", ServerInfoLimits.MaxNewsTextLength, ref trimmed);
            if (title.Length == 0 && text.Length == 0) return false;

            item = new ServerInfoNews { Date = date, Title = title, Text = text };
            return true;
        }
        catch (InvalidOperationException)
        {
            // A lone surrogate escape in one of this entry's keys.
            return false;
        }
    }

    private static bool TryGetArray(JsonElement root, string key, string path, out JsonElement array)
    {
        if (!root.TryGetProperty(key, out array)) return false;
        if (array.ValueKind == JsonValueKind.Array) return true;

        Logger.Warning("Server info {Key} in {Path} is not an array; skipping it", key, path);
        return false;
    }

    private static string ReadField(JsonElement entry, string name, int maxLength, ref bool trimmed)
    {
        if (!entry.TryGetProperty(name, out JsonElement value) || !TryReadText(value, out string raw)) return string.Empty;

        string text = RemoveControlCharacters(raw);
        if (text.Length <= maxLength) return text;

        trimmed = true;
        return Truncate(text, maxLength);
    }

    // A lone surrogate escape such as \ud83d cannot become a string, so that entry is skipped, not fatal.
    private static bool TryReadText(JsonElement entry, out string text)
    {
        text = string.Empty;
        if (entry.ValueKind != JsonValueKind.String) return false;

        try
        {
            text = entry.GetString();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string RemoveControlCharacters(string text)
    {
        var characters = text.ToCharArray();
        for (int i = 0; i < characters.Length; i++)
        {
            if (char.IsControl(characters[i])) characters[i] = ' ';
        }

        return new string(characters).Trim();
    }

    private static string Truncate(string text, int maxLength)
    {
        int length = maxLength;
        // Cut before a surrogate pair instead of keeping half of it.
        if (char.IsHighSurrogate(text[length - 1])) length--;

        return text.Substring(0, length).TrimEnd();
    }
}
