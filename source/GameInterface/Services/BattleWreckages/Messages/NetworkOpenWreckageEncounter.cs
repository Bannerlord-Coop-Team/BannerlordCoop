using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.BattleWreckages.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkOpenWreckageEncounter : ICommand
{
    [ProtoMember(1)]
    public readonly string BattleWreckageId;

    [ProtoMember(2)]
    public readonly bool IsInterrupted;

    [ProtoMember(3)]
    public readonly CampaignTime RequiredInvestigationDuration;

    public NetworkOpenWreckageEncounter(string battleWreckageId, bool isInterrupted, CampaignTime requiredInvestigationDuration)
    {
        BattleWreckageId = battleWreckageId;
        IsInterrupted = isInterrupted;
        RequiredInvestigationDuration = requiredInvestigationDuration;
    }
}
