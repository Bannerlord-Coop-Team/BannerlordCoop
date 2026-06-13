using Common.Messaging;
using GameInterface.Services.Players.Data;
using ProtoBuf;

namespace Coop.Core.Client.Services.Heroes.Messages;

/// <summary>
/// Snapshot of every player registered on the server, sent peer-targeted to a joining client
/// right after the transfer save. The players' heroes are already inside the save; this carries
/// only the registry records that mark them as player-controlled. Without it the joiner never
/// registers those players: the per-player broadcast fires once, at character creation, and a
/// peer that was not yet eligible for broadcasts at that moment never sees it.
/// </summary>
[ProtoContract]
public readonly struct NetworkExistingPlayers : IEvent
{
    [ProtoMember(1)]
    public readonly Player[] Players;

    public NetworkExistingPlayers(Player[] players)
    {
        Players = players;
    }
}
