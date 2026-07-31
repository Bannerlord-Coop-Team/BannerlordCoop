using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Players.Messages;

/// <summary>
/// Server broadcast: the given player's registration was deleted. Clients drop the player from
/// their own registry and mark its hero dead locally (the server-side kill's state flip does not
/// replicate on its own). Sent BEFORE the party destroy replicates on the same ordered stream,
/// so every client lifts the player-party destroy protection
/// (<see cref="MobileParties.Patches.DestroyPartyActionPatch"/>) before applying it.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerRemoved : IEvent
{
    [ProtoMember(1)]
    public string ControllerId { get; }

    [ProtoMember(2)]
    public string HeroId { get; }

    public NetworkPlayerRemoved(string controllerId, string heroId)
    {
        ControllerId = controllerId;
        HeroId = heroId;
    }
}
