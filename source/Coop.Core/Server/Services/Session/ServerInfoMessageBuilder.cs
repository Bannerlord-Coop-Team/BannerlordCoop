using Common.Logging;
using GameInterface.Configuration;
using GameInterface.Services.UI.ServerInfo;
using Serilog;
using System;
using System.Collections.Generic;

namespace Coop.Core.Server.Services.Session;

/// <summary>Turns the serverInfo block of mod-config.json into the message each joining player gets, within the caps.</summary>
public interface IServerInfoMessageBuilder
{
    /// <summary>The capped info with only the links that pass <see cref="IServerInfoLinkRules"/>, or null when there is nothing to show.</summary>
    NetworkServerInfo Build(ServerInfoConfigData data);
}

/// <inheritdoc cref="IServerInfoMessageBuilder"/>
internal sealed class ServerInfoMessageBuilder : IServerInfoMessageBuilder
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerInfoMessageBuilder>();

    private readonly IServerInfoLinkRules linkRules;

    public ServerInfoMessageBuilder(IServerInfoLinkRules linkRules)
    {
        if (linkRules == null) throw new ArgumentNullException(nameof(linkRules));

        this.linkRules = linkRules;
    }

    public NetworkServerInfo Build(ServerInfoConfigData data)
    {
        if (data == null) return null;

        string[] motd = Texts(data.Motd, "motd", ServerInfoLimits.MaxMotdParagraphs);
        string[] rules = Texts(data.Rules, "rules", ServerInfoLimits.MaxRules);
        ServerInfoLink[] links = Links(data.Links);
        ServerInfoNews[] news = News(data.News);
        if (motd.Length == 0 && rules.Length == 0 && links.Length == 0 && news.Length == 0) return null;

        Logger.Information(
            "Server info loaded from mod-config.json (motd {Motd} paragraph(s), rules {Rules}, links {Links}, news {News})",
            motd.Length,
            rules.Length,
            links.Length,
            news.Length);
        return new NetworkServerInfo(motd, rules, links, news);
    }

    // Blank entries are dropped and the rest are cut to the caps; one warning names a key that was cut.
    private static string[] Texts(string[] entries, string key, int maxCount)
    {
        var texts = new List<string>();
        bool cut = false;
        foreach (string entry in entries ?? Array.Empty<string>())
        {
            string text = Clean(entry);
            if (text.Length == 0) continue;
            if (texts.Count == maxCount)
            {
                cut = true;
                break;
            }

            texts.Add(Truncate(text, ServerInfoLimits.MaxTextLength, ref cut));
        }

        if (cut)
        {
            Logger.Warning(
                "mod-config.json: serverInfo.{Key:l} was cut to {MaxCount} entries of {MaxLength} characters",
                key,
                maxCount,
                ServerInfoLimits.MaxTextLength);
        }

        return texts.ToArray();
    }

    // A link needs an absolute http or https address; the log names its index, not the address.
    private ServerInfoLink[] Links(ServerInfoLink[] entries)
    {
        var links = new List<ServerInfoLink>();
        bool cut = false;
        int count = entries?.Length ?? 0;
        for (int index = 0; index < count; index++)
        {
            ServerInfoLink entry = entries[index];
            if (entry == null || !linkRules.TryNormalize(Clean(entry.Url), out string url))
            {
                Logger.Warning("mod-config.json: serverInfo.links[{Index}] has no absolute http or https url; skipping it", index);
                continue;
            }

            if (links.Count == ServerInfoLimits.MaxLinks)
            {
                cut = true;
                break;
            }

            links.Add(new ServerInfoLink { Label = Truncate(Clean(entry.Label), ServerInfoLimits.MaxLinkLabelLength, ref cut), Url = url });
        }

        if (cut)
        {
            Logger.Warning(
                "mod-config.json: serverInfo.links was cut to {MaxLinks} links with {MaxLabelLength} label characters",
                ServerInfoLimits.MaxLinks,
                ServerInfoLimits.MaxLinkLabelLength);
        }

        return links.ToArray();
    }

    // An entry needs a title or a text; a missing field reads as empty.
    private static ServerInfoNews[] News(ServerInfoNews[] entries)
    {
        var news = new List<ServerInfoNews>();
        bool cut = false;
        foreach (ServerInfoNews entry in entries ?? Array.Empty<ServerInfoNews>())
        {
            if (entry == null) continue;
            string title = Truncate(Clean(entry.Title), ServerInfoLimits.MaxNewsTitleLength, ref cut);
            string text = Truncate(Clean(entry.Text), ServerInfoLimits.MaxTextLength, ref cut);
            if (title.Length == 0 && text.Length == 0) continue;
            if (news.Count == ServerInfoLimits.MaxNews)
            {
                cut = true;
                break;
            }

            string date = Truncate(Clean(entry.Date), ServerInfoLimits.MaxNewsDateLength, ref cut);
            news.Add(new ServerInfoNews { Date = date, Title = title, Text = text });
        }

        if (cut)
        {
            Logger.Warning(
                "mod-config.json: serverInfo.news was cut to {MaxNews} entries with {MaxDate} date, {MaxTitle} title and {MaxText} text characters",
                ServerInfoLimits.MaxNews,
                ServerInfoLimits.MaxNewsDateLength,
                ServerInfoLimits.MaxNewsTitleLength,
                ServerInfoLimits.MaxTextLength);
        }

        return news.ToArray();
    }

    // Control characters become spaces, so a pasted tab or line break cannot break a row.
    private static string Clean(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var characters = text.ToCharArray();
        for (int i = 0; i < characters.Length; i++)
        {
            if (char.IsControl(characters[i])) characters[i] = ' ';
        }

        return new string(characters).Trim();
    }

    // Cuts before a surrogate pair instead of keeping half of it.
    private static string Truncate(string text, int maxLength, ref bool cut)
    {
        if (text.Length <= maxLength) return text;

        cut = true;
        int length = char.IsHighSurrogate(text[maxLength - 1]) ? maxLength - 1 : maxLength;
        return text.Substring(0, length).TrimEnd();
    }
}
