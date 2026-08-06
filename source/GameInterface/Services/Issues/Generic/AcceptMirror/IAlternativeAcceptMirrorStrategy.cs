using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Issues.Generic.AcceptMirror;

public interface IAlternativeAcceptMirrorStrategy<TPayload>
{
    void MirrorAlternativeAccepted(Hero owner, TPayload payload);

    void RejectAcceptance(Hero owner);
}

public readonly struct AlternativeSolutionVanillaState
{
    public readonly CampaignTime ReturnTime;
    public readonly CampaignTime EffectClearTime;
    public readonly float FailureChance;
    public readonly int CasualtyCount;
    public readonly float TotalTroopXpAmount;
    public readonly string CompanionRewardSkillId;

    public AlternativeSolutionVanillaState(
        CampaignTime returnTime, CampaignTime effectClearTime, float failureChance,
        int casualtyCount, float totalTroopXpAmount, string companionRewardSkillId)
    {
        ReturnTime = returnTime;
        EffectClearTime = effectClearTime;
        FailureChance = failureChance;
        CasualtyCount = casualtyCount;
        TotalTroopXpAmount = totalTroopXpAmount;
        CompanionRewardSkillId = companionRewardSkillId;
    }
}

// Vanilla's StartIssueWithAlternativeSolution() sets more than the bare _issueState/IsTriedToSolveBefore
// transition every MirrorAlternativeAccepted also needs: the real return/effect-clear times,
// failure/casualty rolls, companion reward skill, troop XP pool, and a matching journal log entry. Capture
// reads these off whichever peer's genuine call produced them; Apply force-writes them on every other peer -
// letting a second peer independently recompute failure/casualty via its own RNG would desync any future
// instance with AlternativeSolutionScaleFlag.FailureRisk/Casualties (today's 2 instances are Duration-only/
// None, so it's currently a no-op, but the capture is generic so a future flagged instance stays correct).
internal static class AlternativeSolutionVanillaStateSync
{
    private static readonly PropertyInfo ReturnTimeProperty =
        AccessTools.Property(typeof(IssueBase), nameof(IssueBase.AlternativeSolutionReturnTimeForTroops));
    private static readonly PropertyInfo EffectClearTimeProperty =
        AccessTools.Property(typeof(IssueBase), nameof(IssueBase.AlternativeSolutionIssueEffectClearTime));
    private static readonly FieldInfo FailureChanceField = AccessTools.Field(typeof(IssueBase), "_failureChance");
    private static readonly FieldInfo CasualtyCountField = AccessTools.Field(typeof(IssueBase), "_alternativeSolutionCasualtyCount");
    private static readonly FieldInfo CompanionRewardSkillField = AccessTools.Field(typeof(IssueBase), "_companionRewardSkill");
    private static readonly FieldInfo TotalTroopXpAmountField = AccessTools.Field(typeof(IssueBase), "_totalTroopXpAmount");
    private static readonly PropertyInfo StartLogProperty = AccessTools.Property(typeof(IssueBase), "AlternativeSolutionStartLog");
    private static readonly PropertyInfo BaseDurationProperty = AccessTools.Property(typeof(IssueBase), "AlternativeSolutionBaseDurationInDaysInternal");

    public static AlternativeSolutionVanillaState Capture(IssueBase issue) => new(
        (CampaignTime)ReturnTimeProperty.GetValue(issue),
        (CampaignTime)EffectClearTimeProperty.GetValue(issue),
        (float)FailureChanceField.GetValue(issue),
        (int)CasualtyCountField.GetValue(issue),
        (float)TotalTroopXpAmountField.GetValue(issue),
        (CompanionRewardSkillField.GetValue(issue) as SkillObject)?.StringId);

    public static void Apply(IssueBase issue, AlternativeSolutionVanillaState state)
    {
        ReturnTimeProperty.SetValue(issue, state.ReturnTime);
        issue.IssueDueTime = state.ReturnTime;
        EffectClearTimeProperty.SetValue(issue, state.EffectClearTime);
        FailureChanceField.SetValue(issue, state.FailureChance);
        CasualtyCountField.SetValue(issue, state.CasualtyCount);
        TotalTroopXpAmountField.SetValue(issue, state.TotalTroopXpAmount);

        if (state.CompanionRewardSkillId != null &&
            MBObjectManager.Instance?.GetObject<SkillObject>(state.CompanionRewardSkillId) is SkillObject skill)
        {
            CompanionRewardSkillField.SetValue(issue, skill);
        }

        issue.AddLog(new JournalLog(
            CampaignTime.Now,
            (TextObject)StartLogProperty.GetValue(issue),
            new TextObject("{=VFO7rMzK}Return Days"),
            0,
            (int)BaseDurationProperty.GetValue(issue)));
    }
}

public interface IAlternativeSolutionPayloadFreeze<TPayload>
{
    void Freeze(Hero owner, TPayload payload);

    bool TryGetFrozen(Hero owner, out TPayload payload);
}

internal sealed class PendingRegistryPayloadFreeze<TPayload> : IAlternativeSolutionPayloadFreeze<TPayload>
{
    private readonly PendingRegistry<TPayload> _registry = new();

    public void Freeze(Hero owner, TPayload payload) => _registry.Set(owner, payload);
    public bool TryGetFrozen(Hero owner, out TPayload payload) => _registry.TryGet(owner, out payload);
    public void Clear(Hero owner) => _registry.Clear(owner);
    public void ClearAll() => _registry.ClearAll();

    public System.Collections.Generic.IReadOnlyCollection<System.Collections.Generic.KeyValuePair<Hero, TPayload>> Snapshot() => _registry.Snapshot();

    public void RestoreAll(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<Hero, TPayload>> entries) => _registry.RestoreAll(entries);
}

public sealed class AlternativeAcceptMirrorHandler<TPayload>
{
    private readonly IAlternativeAcceptMirrorStrategy<TPayload> _strategy;

    public AlternativeAcceptMirrorHandler(IAlternativeAcceptMirrorStrategy<TPayload> strategy)
    {
        _strategy = strategy;
    }

    public void Mirror(Hero owner, TPayload payload) => _strategy.MirrorAlternativeAccepted(owner, payload);

    public void Reject(Hero owner) => _strategy.RejectAcceptance(owner);
}
