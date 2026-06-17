using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

/// <summary>
/// Client -&gt; Server: this client is showing the "hold on" popup for the given (its own) defender party. Lets the
/// server learn the defender's peer so that, if the defender disconnects, it can end the conversation and make the
/// attacker leave the encounter (the request that opened it came from the attacker, so the server otherwise has no
/// peer for the defender).
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPvpDefenderShown : ICommand
{
    [ProtoMember(1)]
    public readonly string DefenderPartyId;

    public NetworkPvpDefenderShown(string defenderPartyId)
    {
        DefenderPartyId = defenderPartyId;
    }
}
