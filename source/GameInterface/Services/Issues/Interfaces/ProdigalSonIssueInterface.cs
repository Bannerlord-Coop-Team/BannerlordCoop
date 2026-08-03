using Common.Util;
using HarmonyLib;
using SandBox.Issues;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection access <see cref="Patches.ProdigalSonIssueCreationPatch"/>/
/// <see cref="Handlers.ProdigalSonIssueHandler"/> need to capture and authoritatively replicate a
/// <see cref="ProdigalSonIssueBehavior.ProdigalSonIssue"/> - the survey's "no accept-time RNG" characterization
/// was about the QUEST/accept path only, and holds there (see <see cref="Patches.ProdigalSonOwnershipGatePatches"/>'s
/// doc comment), but it missed that the ISSUE's own CREATION genuinely rolls twice:
/// <c>Extensions.GetRandomElementWithPredicate&lt;Hero&gt;(issueOwner.Clan.AliveLords, ...)</c> picks
/// <c>_prodigalSon</c>, and <c>SettlementHelper.FindRandomSettlement(...)</c> (feeding into
/// <c>_targetHero</c>, the first gang leader found there) picks the target settlement/gang-leader hero - both
/// genuine creation-time dice rolls, the same shape as Lord Needs Horses' mount-item roll. Both are captured
/// once here and handed to every other peer's replicated instance via the real (already-taking-both-as-ctor-args)
/// constructor - no reflection force-write needed for these two, since (unlike Lord Needs Horses) the ctor
/// doesn't roll them itself, it just takes them as arguments (same shape as Nearby Bandit Base's target hideout).
///
/// The ctor's own <c>DisableHeroAction.Apply(_prodigalSon)</c> side effect (creation-time, self-reversing via
/// <c>OnIssueFinalized</c> regardless of outcome - see the type's own decompiled source) fires identically on
/// every peer's own replicated construction, since it's inside the real ctor body reached by
/// <see cref="ConstructReplicated"/> below - no special handling needed for it here.
///
/// <c>GenerateIssueQuest</c> reads only <c>_targetHero</c>/<c>_prodigalSon</c>/<c>_targetHouse</c> (all
/// deterministic once the two heroes above are captured/forced - <c>_targetHouse</c> is derived from
/// <c>_targetHero.CurrentSettlement</c> inside the ISSUE's own ctor, identically on every peer) plus
/// <c>IssueDifficultyMultiplier</c> (a genuinely per-client value at accept time - see
/// <see cref="Patches.ProdigalSonOwnershipGatePatches"/>'s doc comment for why that one divergence is an
/// accepted, purely-cosmetic trade-off, same reasoning already established for
/// <see cref="VillageNeedsToolsIssueInterface"/>'s own <c>_issueDifficultyMultiplier</c>) - so the quest-solution
/// accept path otherwise rides the fully generic mirror (see <see cref="GenericAcceptMirrorIssueTypes"/>)
/// unchanged.
/// </summary>
public interface IProdigalSonIssueInterface : IGameAbstraction
{
    bool TryCaptureFields(ProdigalSonIssueBehavior.ProdigalSonIssue issue, out Hero prodigalSon, out Hero targetHero);

    ProdigalSonIssueBehavior.ProdigalSonIssue ConstructReplicated(Hero owner, Hero prodigalSon, Hero targetHero);

    void RegisterReplicated(Hero owner, ProdigalSonIssueBehavior.ProdigalSonIssue issue);
}

/// <inheritdoc cref="IProdigalSonIssueInterface"/>
public class ProdigalSonIssueInterface : IProdigalSonIssueInterface
{
    private static readonly FieldInfo ProdigalSonField =
        AccessTools.Field(typeof(ProdigalSonIssueBehavior.ProdigalSonIssue), "_prodigalSon");
    private static readonly FieldInfo TargetHeroField =
        AccessTools.Field(typeof(ProdigalSonIssueBehavior.ProdigalSonIssue), "_targetHero");

    public bool TryCaptureFields(ProdigalSonIssueBehavior.ProdigalSonIssue issue, out Hero prodigalSon, out Hero targetHero)
    {
        prodigalSon = null;
        targetHero = null;
        if (issue == null) return false;

        prodigalSon = (Hero)ProdigalSonField.GetValue(issue);
        targetHero = (Hero)TargetHeroField.GetValue(issue);
        return prodigalSon != null && targetHero != null;
    }

    public ProdigalSonIssueBehavior.ProdigalSonIssue ConstructReplicated(Hero owner, Hero prodigalSon, Hero targetHero)
    {
        // The real ctor already takes both heroes directly (no independent roll to override) - construct
        // normally. This also re-applies DisableHeroAction.Apply(prodigalSon) for real on this peer, exactly as
        // it did on the server - see the type doc comment.
        return new ProdigalSonIssueBehavior.ProdigalSonIssue(owner, prodigalSon, targetHero);
    }

    public void RegisterReplicated(Hero owner, ProdigalSonIssueBehavior.ProdigalSonIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(ProdigalSonIssueBehavior.ProdigalSonIssue), IssueBase.IssueFrequency.Common);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }
}
