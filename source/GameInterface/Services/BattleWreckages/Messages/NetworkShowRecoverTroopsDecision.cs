using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.BattleWreckages.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkShowRecoverTroopsDecision : ICommand
{
    [ProtoMember(1)]
    public readonly int RecoverTroopCount;

    [ProtoMember(2)]
    public readonly CampaignTime RequiredInvestigationDuration;

    public NetworkShowRecoverTroopsDecision(
        int recoverTroopCount,
        CampaignTime requiredInvestigationDuration)
    {
        RecoverTroopCount = recoverTroopCount;
        RequiredInvestigationDuration = requiredInvestigationDuration;
    }
}
