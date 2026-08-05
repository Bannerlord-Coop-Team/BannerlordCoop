using Common.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection access <see cref="Patches.HeadmanNeedsToDeliverAHerdIssueCreationPatch"/>/
/// <see cref="Handlers.HeadmanNeedsToDeliverAHerdIssueHandler"/> need to capture and authoritatively replicate
/// a <see cref="HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue"/>.
///
/// Three CHAINED creation-time rolls (one more hop than e.g.
/// <see cref="HeadmanVillageNeedsDraughtAnimalsIssueInterface"/>'s single <c>_selectedAnimal</c> roll), all in
/// the Issue's own ctor (see the decompiled source): <c>_targetSettlement</c> is picked first
/// (<c>SettlementHelper.FindRandomSettlement</c>, falling back to <c>IssueSettlement.Village.Bound</c>), then
/// <c>_herdTypeToDeliver</c> (an independent <c>_possibleHerdTypes.GetRandomElement()</c> roll, not itself
/// dependent on the settlement), then <c>_targetHero</c> (dependent on the FIRST roll -
/// <c>_targetSettlement.Notables.GetRandomElementWithPredicate(...) ?? _targetSettlement.Notables.GetRandomElement()</c>).
/// All three must be captured off the server's real roll and force-written on every client, in that order,
/// so a client's own independent re-roll of the ctor never diverges from the server for any of the three.
///
/// <c>AnimalCountToDeliver</c>/<c>RewardGold</c> are pure functions of <c>_herdTypeToDeliver.Value</c> plus
/// <c>IssueDifficultyMultiplier</c> (itself a deterministic function of shared campaign state, never an
/// ambient roll) - once these three fields are forced, they compute byte-identically on every peer whenever
/// evaluated, with no further capture needed. Since every field <c>GenerateIssueQuest</c> reads is therefore
/// already frozen by creation time, this type needs NO bespoke accept-time capture at all - see
/// <see cref="GenericAcceptMirrorIssueTypes"/>, which lists it in both eligible sets, so its entire
/// accept/accept-race/ownership-recording path already rides the fully generic
/// <see cref="Patches.IssueQuestAcceptancePatch"/>/<see cref="Patches.IssueWithAlternativeSolutionAcceptancePatch"/>/
/// <see cref="Handlers.VillageNeedsToolsIssueHandler"/>/<see cref="VillageNeedsToolsIssueInterface"/> machinery
/// unchanged, including the already-fixed accept-race <see cref="VillageNeedsToolsIssueInterface.RejectAcceptance"/>
/// (this type deliberately has NO bespoke RejectAcceptance of its own to reintroduce that bug in).
/// </summary>
public interface IHeadmanNeedsToDeliverAHerdIssueInterface : IGameAbstraction
{
    bool TryCaptureFields(
        HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue issue,
        out Settlement targetSettlement,
        out Hero targetHero,
        out ItemObject herdTypeToDeliver);

    HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue ConstructReplicated(
        Hero owner, Settlement targetSettlement, Hero targetHero, ItemObject herdTypeToDeliver);

    void RegisterReplicated(Hero owner, HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue issue);
}

/// <inheritdoc cref="IHeadmanNeedsToDeliverAHerdIssueInterface"/>
public class HeadmanNeedsToDeliverAHerdIssueInterface : IHeadmanNeedsToDeliverAHerdIssueInterface
{
    private static readonly FieldInfo TargetSettlementField =
        AccessTools.Field(typeof(HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue), "_targetSettlement");
    private static readonly FieldInfo TargetHeroField =
        AccessTools.Field(typeof(HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue), "_targetHero");
    private static readonly FieldInfo HerdTypeToDeliverField =
        AccessTools.Field(typeof(HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue), "_herdTypeToDeliver");

    public bool TryCaptureFields(
        HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue issue,
        out Settlement targetSettlement,
        out Hero targetHero,
        out ItemObject herdTypeToDeliver)
    {
        targetSettlement = null;
        targetHero = null;
        herdTypeToDeliver = null;
        if (issue == null) return false;

        targetSettlement = (Settlement)TargetSettlementField.GetValue(issue);
        targetHero = (Hero)TargetHeroField.GetValue(issue);
        herdTypeToDeliver = (ItemObject)HerdTypeToDeliverField.GetValue(issue);

        return targetSettlement != null && targetHero != null && herdTypeToDeliver != null;
    }

    public HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue ConstructReplicated(
        Hero owner, Settlement targetSettlement, Hero targetHero, ItemObject herdTypeToDeliver)
    {
        // The public ctor is the only way to build one, and it independently re-rolls all three chained
        // fields (FindRandomSettlement, then GetRandomElement, then GetRandomElementWithPredicate/
        // GetRandomElement again off the just-rolled settlement's own Notables). Build it normally for
        // everything else it sets up, then force all three to the server's authoritative values.
        var issue = new HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue(owner);

        TargetSettlementField.SetValue(issue, targetSettlement);
        TargetHeroField.SetValue(issue, targetHero);
        HerdTypeToDeliverField.SetValue(issue, herdTypeToDeliver);

        return issue;
    }

    public void RegisterReplicated(Hero owner, HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue), IssueBase.IssueFrequency.VeryCommon);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }
}
