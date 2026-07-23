using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

/// <summary>
/// Server -&gt; all clients: another player (the attacker) has opened a player-vs-player interaction with the party
/// identified by <see cref="DefenderPartyId"/>. The client controlling that party shows a blocking "hold on" popup
/// (<see cref="Handlers.PvPInteractionClientHandler"/>); every other client ignores it. Paired with
/// <see cref="NetworkPlayerInteractionEnded"/>.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerInteractionStarted : ICommand
{
    [ProtoMember(1)]
    public readonly string DefenderPartyId;
    [ProtoMember(2)]
    public readonly string AttackerName;
    [ProtoMember(3)]
    public readonly bool IsLocationInteraction;

    public NetworkPlayerInteractionStarted(string defenderPartyId, string attackerName, bool isLocationInteraction = false)
    {
        DefenderPartyId = defenderPartyId;
        AttackerName = attackerName;
        IsLocationInteraction = isLocationInteraction;
    }
}
