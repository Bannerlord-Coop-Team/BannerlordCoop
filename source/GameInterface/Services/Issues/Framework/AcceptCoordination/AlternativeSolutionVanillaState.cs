using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

/// <summary>
/// What vanilla's StartIssueWithAlternativeSolution rolls or derives from the clock, taken from the
/// server's run so every machine ends up with the same values.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct AlternativeSolutionVanillaState
{
    [ProtoMember(1)]
    public readonly float DifficultyMultiplier;

    [ProtoMember(2)]
    public readonly float FailureChance;

    [ProtoMember(3)]
    public readonly int CasualtyCount;

    [ProtoMember(4)]
    public readonly string CompanionRewardSkillId;

    [ProtoMember(5)]
    public readonly float TotalTroopXpAmount;

    [ProtoMember(6)]
    public readonly long ReturnTimeTicks;

    [ProtoMember(7)]
    public readonly long EffectClearTimeTicks;

    [ProtoMember(8)]
    public readonly long IssueDueTimeTicks;

    public AlternativeSolutionVanillaState(
        float difficultyMultiplier,
        float failureChance,
        int casualtyCount,
        string companionRewardSkillId,
        float totalTroopXpAmount,
        long returnTimeTicks,
        long effectClearTimeTicks,
        long issueDueTimeTicks)
    {
        DifficultyMultiplier = difficultyMultiplier;
        FailureChance = failureChance;
        CasualtyCount = casualtyCount;
        CompanionRewardSkillId = companionRewardSkillId;
        TotalTroopXpAmount = totalTroopXpAmount;
        ReturnTimeTicks = returnTimeTicks;
        EffectClearTimeTicks = effectClearTimeTicks;
        IssueDueTimeTicks = issueDueTimeTicks;
    }
}
