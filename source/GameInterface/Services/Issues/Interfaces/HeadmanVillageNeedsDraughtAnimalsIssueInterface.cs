using Common.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection access <see cref="Patches.HeadmanVillageNeedsDraughtAnimalsIssueCreationPatch"/>/
/// <see cref="Patches.HeadmanVillageNeedsDraughtAnimalsAcceptancePatches"/>/
/// <see cref="Handlers.HeadmanVillageNeedsDraughtAnimalsIssueHandler"/> need to capture and authoritatively
/// replicate a <see cref="HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue"/>.
/// The survey's ONLY flag was the accept-time <c>_discountValue</c> roll - correct, but it missed a genuine
/// CREATION-time roll too: the Issue's own ctor calls <c>_possibleAnimals.GetRandomElement()</c> to pick
/// <c>_selectedAnimal</c> (the same shape as Village Needs Crafting Materials' requested-item roll/Lord Needs
/// Horses' mount roll), and ALSO derives <c>_requestedAnimalAmount</c> in that same ctor from
/// <c>base.IssueDifficultyMultiplier</c> (per-client at CREATION time - unlike Landowner Needs Manual
/// Laborers' equivalent, this one is stored as a plain field at ctor time and never re-derived later, so a
/// one-shot creation-time capture is enough; <c>_isQuestWithMeatOffer</c> reads live village Hearth at that
/// same creation moment too and is captured alongside for the same reason).
///
/// <c>OfferedMeatAmount</c>/<c>RewardGold</c> are pure functions of these three forced fields (plus a shared
/// static item's <c>Value</c>) - once forced, they compute byte-identically on every peer whenever evaluated,
/// with no further capture needed.
/// </summary>
public interface IHeadmanVillageNeedsDraughtAnimalsIssueInterface : IGameAbstraction
{
    bool TryCaptureFields(
        HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue issue,
        out ItemObject selectedAnimal,
        out int requestedAnimalAmount,
        out bool isQuestWithMeatOffer);

    HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue ConstructReplicated(
        Hero owner, ItemObject selectedAnimal, int requestedAnimalAmount, bool isQuestWithMeatOffer);

    void RegisterReplicated(Hero owner, HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue issue);

    void ReplayQuestAccepted(Hero owner);

    bool TryCaptureQuestFields(Hero owner, out int discountValue);

    void MirrorQuestAccepted(Hero owner, int discountValue);
}

/// <inheritdoc cref="IHeadmanVillageNeedsDraughtAnimalsIssueInterface"/>
public class HeadmanVillageNeedsDraughtAnimalsIssueInterface : IHeadmanVillageNeedsDraughtAnimalsIssueInterface
{
    private static readonly FieldInfo SelectedAnimalField =
        AccessTools.Field(typeof(HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue), "_selectedAnimal");
    private static readonly FieldInfo RequestedAnimalAmountField =
        AccessTools.Field(typeof(HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue), "_requestedAnimalAmount");
    private static readonly FieldInfo IsQuestWithMeatOfferField =
        AccessTools.Field(typeof(HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue), "_isQuestWithMeatOffer");
    private static readonly FieldInfo DiscountValueField =
        AccessTools.Field(typeof(HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssueQuest), "_discountValue");

    public bool TryCaptureFields(
        HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue issue,
        out ItemObject selectedAnimal,
        out int requestedAnimalAmount,
        out bool isQuestWithMeatOffer)
    {
        selectedAnimal = null;
        requestedAnimalAmount = 0;
        isQuestWithMeatOffer = false;
        if (issue == null) return false;

        selectedAnimal = (ItemObject)SelectedAnimalField.GetValue(issue);
        requestedAnimalAmount = (int)RequestedAnimalAmountField.GetValue(issue);
        isQuestWithMeatOffer = (bool)IsQuestWithMeatOfferField.GetValue(issue);
        return selectedAnimal != null;
    }

    public HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue ConstructReplicated(
        Hero owner, ItemObject selectedAnimal, int requestedAnimalAmount, bool isQuestWithMeatOffer)
    {
        // The public ctor is the only way to build one, and it independently re-rolls _selectedAnimal via
        // GetRandomElement() and re-derives _requestedAnimalAmount/_isQuestWithMeatOffer from this machine's
        // own IssueDifficultyMultiplier/live village Hearth. Build it normally for everything else it sets up,
        // then force these three fields to the server's authoritative values.
        var issue = new HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue(owner);

        SelectedAnimalField.SetValue(issue, selectedAnimal);
        RequestedAnimalAmountField.SetValue(issue, requestedAnimalAmount);
        IsQuestWithMeatOfferField.SetValue(issue, isQuestWithMeatOffer);

        return issue;
    }

    public void RegisterReplicated(Hero owner, HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue), IssueBase.IssueFrequency.VeryCommon);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }

    public void ReplayQuestAccepted(Hero owner)
    {
        if (owner?.Issue is not HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue || !owner.Issue.IsOngoingWithoutQuest) return;

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.StartIssueQuest(owner);
        }
    }

    public bool TryCaptureQuestFields(Hero owner, out int discountValue)
    {
        discountValue = 0;

        if (owner?.Issue?.IssueQuest is not HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssueQuest quest) return false;

        discountValue = (int)DiscountValueField.GetValue(quest);
        return true;
    }

    public void MirrorQuestAccepted(Hero owner, int discountValue)
    {
        if (owner?.Issue is not HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue) return;

        using (new AllowedThread())
        {
            if (owner.Issue.IsOngoingWithoutQuest)
            {
                Campaign.Current.IssueManager.StartIssueQuest(owner);
            }

            if (owner.Issue.IssueQuest is not HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssueQuest quest) return;

            DiscountValueField.SetValue(quest, discountValue);
        }
    }
}
