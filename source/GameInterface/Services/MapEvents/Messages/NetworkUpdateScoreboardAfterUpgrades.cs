using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkUpdateScoreboardAfterUpgrades : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly string AffectorCharacterId;
    [ProtoMember(3)]
    public readonly string AffectorPartyId;
    [ProtoMember(4)]
    public readonly BattleSideEnum AffectorAgentSide;
    [ProtoMember(5)]
    public readonly int UpgradedCount;

    public NetworkUpdateScoreboardAfterUpgrades(
        string mapEventId,
        string affectorCharacterId,
        string affectorPartyId,
        BattleSideEnum affectorAgentSide,
        int upgradedCount)
    {
        MapEventId = mapEventId;
        AffectorCharacterId = affectorCharacterId;
        AffectorPartyId = affectorPartyId;
        AffectorAgentSide = affectorAgentSide;
        UpgradedCount = upgradedCount;
    }
}
