using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Services.Heroes.Patches;
using Behavior = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior;
using Quest = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArtisanProducts.Patches;

[HarmonyPatch]
internal class ArtisanProductsWorldEventPatches
{
    [HarmonyPatch(typeof(Behavior), nameof(Behavior.OnCheckForIssue))]
    [HarmonyPrefix]
    private static bool Generation() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Behavior), nameof(Behavior.SelectTargetSettlement))]
    [HarmonyPrefix]
    private static bool Target(Hero issueGiver, ref Settlement __result)
    {
        if (ContainerProvider.TryResolve<ArtisanProductsCreationCaptureStrategy>(out var creation) &&
            creation.TryGetCreation(issueGiver, out var target, out _))
        {
            __result = target;
            return false;
        }
        // Vanilla searches from MainParty, which the dedicated server does not have.
        __result = ArtisanProductsCreationCaptureStrategy.SelectTargetSettlement(issueGiver);
        return false;
    }

    [HarmonyPatch(typeof(Behavior), nameof(Behavior.SelectCounterOfferHero))]
    [HarmonyPrefix]
    private static bool Merchant(Hero issueGiver, ref Hero __result)
    {
        if (!ContainerProvider.TryResolve<ArtisanProductsCreationCaptureStrategy>(out var creation) ||
            !creation.TryGetCreation(issueGiver, out _, out var merchant)) return true;
        __result = merchant;
        return false;
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnWarDeclared))]
    [HarmonyPostfix]
    private static void War(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
        => Run(quest => quest.OnWarDeclared(faction1, faction2, declareWarDetail));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnClanChangedKingdom))]
    [HarmonyPostfix]
    private static void Kingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom,
        ChangeKingdomAction.ChangeKingdomActionDetail actionDetail, bool showNotification)
        => Run(quest => quest.OnClanChangedKingdom(clan, oldKingdom, newKingdom, actionDetail, showNotification));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroKilled))]
    [HarmonyPostfix]
    private static void Death(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        => Run(quest => quest.OnHeroKilled(victim, killer, detail, showNotification));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnIssueUpdated))]
    [HarmonyPrefix]
    private static void CompanionJournal(IssueBase issue, IssueBase.IssueUpdateDetails details)
    {
        if (ModInformation.IsServer && issue is Behavior.ArtisanCantSellProductsAtAFairPriceIssue && issue.IsSolvingWithAlternative &&
            (details == IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest || details == IssueBase.IssueUpdateDetails.SentTroopsFailedQuest ||
             details == IssueBase.IssueUpdateDetails.IssueCancel) && ContainerProvider.TryResolve<IArtisanProductsProgress>(out var progress))
            progress.SendIssue(issue, details);
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.BeforeGameMenuOpened))]
    [HarmonyPrefix]
    private static bool Menu() => ModInformation.IsClient;

    private static void Run(Action<Quest> action)
    {
        if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)) dispatcher.RunAsOwners(action);
    }
}

[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.OnHeroKilled))]
internal class ArtisanProductsGiverDeathPatch
{
    [HarmonyPrefix]
    private static bool Before(Hero victim, out MainHeroSubstitutionScope __state)
    {
        __state = null;
        if (!(victim.Issue is Behavior.ArtisanCantSellProductsAtAFairPriceIssue issue)) return true;
        if (ModInformation.IsClient) return false;
        if (ContainerProvider.TryResolve<IIssueOwnerResolver>(out var resolver) && resolver.TryResolveOwnerHero(issue, out var owner))
            __state = new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo);
        return true;
    }

    [HarmonyFinalizer]
    private static void After(MainHeroSubstitutionScope __state) => __state?.Dispose();
}

[HarmonyPatch]
internal class ArtisanProductsServerEventPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> Targets()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnWarDeclared));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnClanChangedKingdom));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnHeroKilled));
    }

    [HarmonyPrefix]
    private static bool Before() => ModInformation.IsServer;
}
