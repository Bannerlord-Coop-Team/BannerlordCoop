using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.ServerInfo;

/// <summary>Server-side caps that keep one message of the day small on the wire and on screen.</summary>
public static class ServerInfoLimits
{
    public const int MaxParagraphs = 10;
    public const int MaxLength = 2000;
}

/// <summary>Carries the whole message of the day to one joining player; each entry is a paragraph.</summary>
[ProtoContract(SkipConstructor = true)]
public sealed record NetworkServerInfo : IMessage
{
    [ProtoMember(1)] public string[] Paragraphs { get; }

    // Holds the operator's text as written after the server's caps.
    public NetworkServerInfo(string[] paragraphs) => Paragraphs = paragraphs;
}
