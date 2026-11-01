using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Naval.CampaignBehaviors.Messages;

internal readonly struct NavalPatrolSettlementChanged : IEvent
{
    public readonly Settlement Settlement;
    public readonly bool IsQueued;
    public readonly CampaignTime QueueTime;
    public readonly MobileParty PatrolParty;

    public NavalPatrolSettlementChanged(
        Settlement settlement,
        bool isQueued,
        CampaignTime queueTime,
        MobileParty patrolParty)
    {
        Settlement = settlement;
        IsQueued = isQueued;
        QueueTime = queueTime;
        PatrolParty = patrolParty;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNavalPatrolSettlementChanged : ICommand
{
    [ProtoMember(1)]
    public readonly uint SettlementHandle;

    [ProtoMember(2)]
    public readonly bool IsQueued;

    [ProtoMember(3)]
    public readonly CampaignTime QueueTime;

    [ProtoMember(4)]
    public readonly uint PatrolPartyHandle;

    public NetworkNavalPatrolSettlementChanged(
        uint settlementHandle,
        bool isQueued,
        CampaignTime queueTime,
        uint patrolPartyHandle)
    {
        SettlementHandle = settlementHandle;
        IsQueued = isQueued;
        QueueTime = queueTime;
        PatrolPartyHandle = patrolPartyHandle;
    }
}
