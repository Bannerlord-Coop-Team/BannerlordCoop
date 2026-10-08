using GameInterface.Services.Issues.Generic.AcceptMirror;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Data;

[ProtoContract(SkipConstructor = true)]
internal readonly struct ExtortionQuestAcceptFields
{
    [ProtoMember(1)] public readonly string PlayerHeroId;
    [ProtoMember(2)] public readonly string DeserterPartyId;
    [ProtoMember(3)] public readonly string QuestId;
    [ProtoMember(4)] public readonly float Difficulty;
    [ProtoMember(5)] public readonly int RewardGold;
    [ProtoMember(6)] public readonly CampaignTime DueTime;

    public ExtortionQuestAcceptFields(string playerHeroId, string deserterPartyId, string questId,
        float difficulty, int rewardGold, CampaignTime dueTime)
    {
        PlayerHeroId = playerHeroId;
        DeserterPartyId = deserterPartyId;
        QuestId = questId;
        Difficulty = difficulty;
        RewardGold = rewardGold;
        DueTime = dueTime;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct ExtortionAlternativeAcceptFields
{
    [ProtoMember(1)] public readonly float Difficulty;
    [ProtoMember(2)] public readonly AlternativeSolutionVanillaState State;

    public ExtortionAlternativeAcceptFields(float difficulty, AlternativeSolutionVanillaState state)
    {
        Difficulty = difficulty;
        State = state;
    }
}
