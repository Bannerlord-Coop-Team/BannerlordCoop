using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Hideouts.Messages;

internal enum HideoutCampaignConsequence
{
    PrepareMission,
    SetAttackCooldown,
    GrantClearRewards,
    PrepareDirectAssaultMission,
}

internal readonly struct HideoutCampaignConsequenceRequested : IEvent
{
    public readonly Settlement Settlement;
    public readonly HideoutCampaignConsequence Consequence;

    public HideoutCampaignConsequenceRequested(
        Settlement settlement,
        HideoutCampaignConsequence consequence)
    {
        Settlement = settlement;
        Consequence = consequence;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkHideoutCampaignConsequenceRequested : ICommand
{
    [ProtoMember(1)]
    public readonly string SettlementId;

    [ProtoMember(2)]
    public readonly HideoutCampaignConsequence Consequence;

    [ProtoMember(3)]
    public readonly string RequestId;

    public NetworkHideoutCampaignConsequenceRequested(
        string settlementId,
        HideoutCampaignConsequence consequence,
        string requestId = null)
    {
        SettlementId = settlementId;
        Consequence = consequence;
        RequestId = requestId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkHideoutCampaignConsequenceResolved : ICommand
{
    [ProtoMember(1)]
    public readonly string RequestId;

    [ProtoMember(2)]
    public readonly bool Accepted;

    [ProtoMember(3)]
    public readonly int ExpectedHealthyDefenderCount;

    public NetworkHideoutCampaignConsequenceResolved(
        string requestId,
        bool accepted,
        int expectedHealthyDefenderCount)
    {
        RequestId = requestId;
        Accepted = accepted;
        ExpectedHealthyDefenderCount = expectedHealthyDefenderCount;
    }
}
