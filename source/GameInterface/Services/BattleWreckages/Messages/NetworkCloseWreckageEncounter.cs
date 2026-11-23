using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.BattleWreckages.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCloseWreckageEncounter : ICommand
{
    [ProtoMember(1)]
    public readonly BattleWreckageCampaignBehavior.WreckageLeaveReason Reason;

    public NetworkCloseWreckageEncounter(BattleWreckageCampaignBehavior.WreckageLeaveReason reason)
    {
        Reason = reason;
    }
}
