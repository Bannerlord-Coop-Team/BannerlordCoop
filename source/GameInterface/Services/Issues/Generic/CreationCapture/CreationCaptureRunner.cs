using Common.Util;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic.CreationCapture;

/// <summary>
/// Thin generic driver over <see cref="ICreationCaptureStrategy{TIssue,TFields}"/>, modeling the shared shape
/// behind every per-type creation-capture postfix in this codebase: a genuine server-side
/// <see cref="IssueManager.CreateNewIssue"/> captures the rolled fields once and publishes them once; a
/// non-creating peer constructs its own replicated instance from the broadcast once. Callers still write their
/// own concrete Harmony <c>[HarmonyPatch]</c> shell and message types - this only factors out the
/// capture/construct calls themselves so a migrated type's own postfix/handler doesn't need to know the
/// reflection details.
///
/// <paramref name="frequency"/> defaults to <see cref="IssueBase.IssueFrequency.Rare"/> - a different migrated
/// type may need a different real vanilla frequency.
/// </summary>
public sealed class CreationCaptureRunner<TIssue, TFields> where TIssue : IssueBase
{
    private readonly ICreationCaptureStrategy<TIssue, TFields> _strategy;
    private readonly IssueBase.IssueFrequency _frequency;

    public CreationCaptureRunner(ICreationCaptureStrategy<TIssue, TFields> strategy, IssueBase.IssueFrequency frequency = IssueBase.IssueFrequency.Rare)
    {
        _strategy = strategy;
        _frequency = frequency;
    }

    /// <summary>[Genuine server-side creation] Captures the just-rolled fields off <paramref name="issue"/>.
    /// Returns false (nothing to broadcast) if capture fails - the caller should log and bail, same as every
    /// real precedent does today.</summary>
    public bool TryCapture(TIssue issue, out TFields fields) => _strategy.TryCaptureFields(issue, out fields);

    /// <summary>[Non-creating peer] Constructs a byte-identical replicated instance from broadcast fields, then
    /// registers it with <see cref="Campaign.Current"/>'s <see cref="IssueManager"/> via the same
    /// <see cref="PotentialIssueData"/> "hand back an already-built instance instead of constructing a new one"
    /// technique every real replicated-construction call site already uses.
    ///
    /// <paramref name="afterRegistered"/> is an escape hatch: optional, runs AFTER
    /// <see cref="IssueManager.CreateNewIssue"/>'s own bookkeeping (including any <c>AfterIssueCreation()</c>
    /// override) has already completed, for the rare field that override itself overwrites - forcing it
    /// beforehand (the normal, <see cref="ICreationCaptureStrategy{TIssue,TFields}.ConstructReplicated"/> order)
    /// would be silently clobbered. Null (the default) for types with no such hazard.</summary>
    public TIssue ConstructAndRegisterReplicated(Hero owner, TFields fields, Action<TIssue, TFields> afterRegistered = null)
    {
        var issue = _strategy.ConstructReplicated(owner, fields);
        RegisterReplicated(owner, issue);
        afterRegistered?.Invoke(issue, fields);
        return issue;
    }

    private void RegisterReplicated(Hero owner, TIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(TIssue), _frequency);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }
}
