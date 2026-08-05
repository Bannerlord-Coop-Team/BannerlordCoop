using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Hideouts.Messages;

internal enum HideoutCampaignConsequence
{
    PrepareMission,
    SetAttackCooldown,
    GrantClearRewards,
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

[ProtoContract]
internal readonly struct NetworkHideoutCampaignConsequenceRequested : ICommand
{
    [ProtoMember(1)]
    public readonly string SettlementId;

    [ProtoMember(2)]
    public readonly HideoutCampaignConsequence Consequence;

    public NetworkHideoutCampaignConsequenceRequested(
        string settlementId,
        HideoutCampaignConsequence consequence)
    {
        SettlementId = settlementId;
        Consequence = consequence;
    }
}
