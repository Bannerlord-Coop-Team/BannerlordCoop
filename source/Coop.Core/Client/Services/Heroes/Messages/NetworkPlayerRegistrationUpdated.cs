using Common.Messaging;
using GameInterface.Services.Players.Data;
using ProtoBuf;

namespace Coop.Core.Client.Services.Heroes.Messages;

/// <summary>
/// Replaces a remote player's party mapping after the server repairs a stale registration.
/// </summary>
[ProtoContract]
public readonly struct NetworkPlayerRegistrationUpdated : IEvent
{
    [ProtoMember(1)]
    public readonly Player Player;

    public NetworkPlayerRegistrationUpdated(Player player)
    {
        Player = player;
    }
}
