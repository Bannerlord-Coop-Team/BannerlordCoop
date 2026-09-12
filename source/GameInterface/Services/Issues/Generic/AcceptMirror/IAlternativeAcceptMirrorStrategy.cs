using HarmonyLib;
using ProtoBuf;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Issues.Generic.AcceptMirror;

public interface IAlternativeAcceptMirrorStrategy<TPayload>
{
    void ReplayAlternativeAccepted(Hero owner);

    bool TryCaptureAlternativeFields(Hero owner, out TPayload payload);

    void MirrorAlternativeAccepted(Hero owner, TPayload payload);

    void RejectAcceptance(Hero owner);
}

[ProtoContract(SkipConstructor = true)]
public readonly struct AlternativeSolutionVanillaState
{
    [ProtoMember(1)]
    public readonly CampaignTime ReturnTime;
    [ProtoMember(2)]
    public readonly CampaignTime EffectClearTime;
    [ProtoMember(3)]
    public readonly float FailureChance;
    [ProtoMember(4)]
    public readonly int CasualtyCount;
    [ProtoMember(5)]
    public readonly float TotalTroopXpAmount;
    [ProtoMember(6)]
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

internal static class AlternativeSolutionVanillaStateSync
{
    private static readonly PropertyInfo StartLogProperty = AccessTools.Property(typeof(IssueBase), "AlternativeSolutionStartLog");
    private static readonly PropertyInfo BaseDurationProperty = AccessTools.Property(typeof(IssueBase), "AlternativeSolutionBaseDurationInDaysInternal");

    public static AlternativeSolutionVanillaState Capture(IssueBase issue) => new(
        issue.AlternativeSolutionReturnTimeForTroops,
        issue.AlternativeSolutionIssueEffectClearTime,
        issue._failureChance,
        issue._alternativeSolutionCasualtyCount,
        issue._totalTroopXpAmount,
        issue._companionRewardSkill?.StringId);

    public static void Apply(IssueBase issue, AlternativeSolutionVanillaState state)
    {
        issue.AlternativeSolutionReturnTimeForTroops = state.ReturnTime;
        issue.IssueDueTime = state.ReturnTime;
        issue.AlternativeSolutionIssueEffectClearTime = state.EffectClearTime;
        issue._failureChance = state.FailureChance;
        issue._alternativeSolutionCasualtyCount = state.CasualtyCount;
        issue._totalTroopXpAmount = state.TotalTroopXpAmount;

        if (state.CompanionRewardSkillId != null &&
            MBObjectManager.Instance?.GetObject<SkillObject>(state.CompanionRewardSkillId) is SkillObject skill)
        {
            issue._companionRewardSkill = skill;
        }

        issue.AddLog(new JournalLog(
            CampaignTime.Now,
            (TextObject)StartLogProperty.GetValue(issue),
            new TextObject("{=VFO7rMzK}Return Days"),
            0,
            (int)BaseDurationProperty.GetValue(issue)));
    }
}

public sealed class AlternativeAcceptMirrorHandler<TPayload>
{
    private readonly IAlternativeAcceptMirrorStrategy<TPayload> _strategy;

    public AlternativeAcceptMirrorHandler(IAlternativeAcceptMirrorStrategy<TPayload> strategy)
    {
        _strategy = strategy;
    }

    public bool TryArbitrate(Hero owner, System.Func<Hero, bool> canAccept, out TPayload payload)
    {
        payload = default;
        if (owner == null || canAccept == null || !canAccept(owner)) return false;

        _strategy.ReplayAlternativeAccepted(owner);
        if (_strategy.TryCaptureAlternativeFields(owner, out payload)) return true;

        _strategy.RejectAcceptance(owner);
        return false;
    }

    public void Mirror(Hero owner, TPayload payload) => _strategy.MirrorAlternativeAccepted(owner, payload);

    public void Reject(Hero owner) => _strategy.RejectAcceptance(owner);
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
