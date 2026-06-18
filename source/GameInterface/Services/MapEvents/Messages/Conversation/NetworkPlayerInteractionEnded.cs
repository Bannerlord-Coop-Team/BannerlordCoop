using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

/// <summary>
/// Server -&gt; all clients: the player-vs-player interaction with the party identified by <see cref="DefenderPartyId"/>
/// is over (the attacker left the encounter or disconnected). The client controlling that party closes its "hold on"
/// popup (<see cref="Handlers.PvPInteractionClientHandler"/>). Paired with <see cref="NetworkPlayerInteractionStarted"/>.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerInteractionEnded : ICommand
{
    [ProtoMember(1)]
    public readonly string DefenderPartyId;

    public NetworkPlayerInteractionEnded(string defenderPartyId)
    {
        DefenderPartyId = defenderPartyId;
    }
}
