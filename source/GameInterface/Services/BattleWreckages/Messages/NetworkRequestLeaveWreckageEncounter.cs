using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.BattleWreckages.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestLeaveWreckageEncounter : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly BattleWreckageCampaignBehavior.WreckageLeaveReason Reason;

    public NetworkRequestLeaveWreckageEncounter(
        string mainHeroId,
        BattleWreckageCampaignBehavior.WreckageLeaveReason reason)
    {
        MainHeroId = mainHeroId;
        Reason = reason;
    }
}
