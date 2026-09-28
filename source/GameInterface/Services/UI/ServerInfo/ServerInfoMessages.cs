using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.ServerInfo;

/// <summary>Server-side caps that keep the server info small on the wire and on screen.</summary>
public static class ServerInfoLimits
{
    public const int MaxMotdParagraphs = 10;
    public const int MaxMotdLength = 2000;
    public const int MaxRules = 20;
    public const int MaxRulesLength = 2000;
    public const int MaxLinks = 8;
    public const int MaxLinkLabelLength = 40;
    public const int MaxLinkUrlLength = 512;
    public const int MaxNews = 10;
    public const int MaxNewsDateLength = 40;
    public const int MaxNewsTitleLength = 80;
    public const int MaxNewsTextLength = 500;
    // Every string in one message together; the caps above keep motd, rules and links under it.
    public const int MaxTotalLength = 12000;
}

/// <summary>One operator link; the server sends only absolute http or https addresses.</summary>
[ProtoContract(SkipConstructor = true)]
public sealed record ServerInfoLink
{
    [ProtoMember(1)] public string Label { get; set; }
    [ProtoMember(2)] public string Url { get; set; }
}

/// <summary>One operator news entry; the date is free text shown as written.</summary>
[ProtoContract(SkipConstructor = true)]
public sealed record ServerInfoNews
{
    [ProtoMember(1)] public string Date { get; set; }
    [ProtoMember(2)] public string Title { get; set; }
    [ProtoMember(3)] public string Text { get; set; }
}

/// <summary>Carries the whole server info to one joining player.</summary>
[ProtoContract(SkipConstructor = true)]
public sealed record NetworkServerInfo : IMessage
{
    [ProtoMember(1)] public string[] Motd { get; }
    [ProtoMember(2)] public string[] Rules { get; }
    [ProtoMember(3)] public ServerInfoLink[] Links { get; }
    [ProtoMember(4)] public ServerInfoNews[] News { get; }

    // Holds the operator's text after the server's caps; an empty section arrives as null.
    public NetworkServerInfo(string[] motd, string[] rules, ServerInfoLink[] links, ServerInfoNews[] news)
    {
        Motd = motd;
        Rules = rules;
        Links = links;
        News = news;
    }
}
