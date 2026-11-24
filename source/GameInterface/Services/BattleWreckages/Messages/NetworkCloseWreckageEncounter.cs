using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.BattleWreckages.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCloseWreckageEncounter : ICommand
{
    [ProtoMember(1)]
    public readonly BattleWreckageCampaignBehavior.WreckageLeaveReason Reason;

    [ProtoMember(2)]
    public readonly CampaignTime RequiredInvestigationDuration;

    public NetworkCloseWreckageEncounter(
        BattleWreckageCampaignBehavior.WreckageLeaveReason reason,
        CampaignTime requiredInvestigationDuration)
    {
        Reason = reason;
        RequiredInvestigationDuration = requiredInvestigationDuration;
    }
}
