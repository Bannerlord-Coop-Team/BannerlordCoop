using Common.Messaging;
using LiteNetLib;
using ProtoBuf;

namespace GameInterface.Services.Players.Messages;

public record PlayerDisconnectRequested : IEvent;

public readonly struct PlayerDeletionStarted : IEvent
{
    public readonly NetPeer Peer;

    public PlayerDeletionStarted(NetPeer peer)
    {
        Peer = peer;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestPlayerDisconnect : ICommand {}
