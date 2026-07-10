using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// Server -&gt; all clients: a siege assault ended in a defender victory. A client whose main party is one of
/// <see cref="DefenderPartyIds"/> (a winning inside defender) parks itself on the siege-defeated menu, because
/// the server tore the SiegeEvent/MapEvent down via replication and so bypassed vanilla's local siege-end
/// routing that would otherwise seat the defender there. Sent AFTER the finalize so it arrives behind the
/// event destroy on the reliable-ordered channel.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPromptSiegeDefenderVictory : ICommand
{
    [ProtoMember(1)]
    public readonly string SettlementId;

    [ProtoMember(2)]
    public readonly string[] DefenderPartyIds;

    public NetworkPromptSiegeDefenderVictory(string settlementId, string[] defenderPartyIds)
    {
        SettlementId = settlementId;
        DefenderPartyIds = defenderPartyIds;
    }
}
