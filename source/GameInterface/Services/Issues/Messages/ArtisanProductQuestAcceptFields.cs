using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct ArtisanProductQuestAcceptFields
{
    [ProtoMember(1)] public readonly int Amount;
    [ProtoMember(2)] public readonly int Reward;
    [ProtoMember(3)] public readonly float Difficulty;
    [ProtoMember(4)] public readonly CampaignTime DueTime;
    [ProtoMember(5)] public readonly CampaignTime StartedAt;
    [ProtoMember(6)] public readonly string ControllerId;

    public ArtisanProductQuestAcceptFields(int amount, int reward, float difficulty, CampaignTime dueTime,
        CampaignTime startedAt, string controllerId)
    {
        Amount = amount;
        Reward = reward;
        Difficulty = difficulty;
        DueTime = dueTime;
        StartedAt = startedAt;
        ControllerId = controllerId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct ArtisanProductAlternativeAcceptFields
{
    [ProtoMember(1)] public readonly float Difficulty;
    [ProtoMember(2)] public readonly CampaignTime StartedAt;

    public ArtisanProductAlternativeAcceptFields(float difficulty, CampaignTime startedAt)
    {
        Difficulty = difficulty;
        StartedAt = startedAt;
    }
}
