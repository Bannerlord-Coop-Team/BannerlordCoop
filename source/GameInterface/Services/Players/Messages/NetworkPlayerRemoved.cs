using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Players.Messages;

/// <summary>
/// Server broadcast: the given player's registration was deleted. Clients drop the player from
/// their own registry and run the native death transition for its hero (the field-sync wire
/// application alone skips the clan/campaign bookkeeping); the party destruction arrives
/// separately through the destroy broadcast. Sent BEFORE the kill/destroy replicate on the same
/// ordered stream, so every client lifts the player-party destroy protection
/// (<see cref="MobileParties.Patches.DestroyPartyActionPatch"/>) before applying them.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkPlayerRemoved : IEvent
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
