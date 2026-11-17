using Common.Messaging;
using GameInterface.Services.Players.Data;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// [Server to client] Answer to <see cref="NetworkClientValidate"/> from a campaign-free session server. An
/// accepted join carries the session id, the owning mod and the mod's opaque payload; a refusal carries a reason.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkSessionJoined : IEvent
{
    [ProtoMember(1)]
    public bool Accepted { get; }
    [ProtoMember(2)]
    public string Reason { get; }
    [ProtoMember(3)]
    public string SessionId { get; }
    [ProtoMember(4)]
    public string ModId { get; }
    [ProtoMember(5)]
    public byte[] Payload { get; }
    [ProtoMember(6)]
    public Player Player { get; }

    public NetworkSessionJoined(bool accepted, string reason, string sessionId, string modId, byte[] payload, Player player)
    {
        Accepted = accepted;
        Reason = reason;
        SessionId = sessionId;
        ModId = modId;
        Payload = payload;
        Player = player;
    }
}
