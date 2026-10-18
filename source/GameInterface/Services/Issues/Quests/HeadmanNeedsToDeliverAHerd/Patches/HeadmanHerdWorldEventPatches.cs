using Common;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Heroes.Patches;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd.Patches;

[HarmonyPatch]
internal class HeadmanHerdWorldEventPatches
{
    private static readonly HashSet<Issue> pendingGiverDeaths = new();

    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.OnHeroKilled))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool GiverKilledPrefix(Hero victim, out IDisposable __state)
    {
        __state = null;
        if (!(victim.Issue is Issue issue)) return true;
        if (ModInformation.IsClient) return false;
        if (issue.IssueQuest == null && !issue.IsSolvingWithAlternative) return true;
        if (!ContainerProvider.TryResolve<IIssueOwnerResolver>(out var owners) || !owners.TryResolveOwnerHero(issue, out var owner)) return false;
        __state = new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo);
        // Keep the owner on the issue until vanilla can return the troops without its global queue.
        if (issue.IsSolvingWithAlternative && !Campaign.Current.Models.IssueModel.CanTroopsReturnFromAlternativeSolution()) return false;
        return true;
    }

    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.OnHeroKilled))]
    [HarmonyFinalizer]
    private static void GiverKilledFinalizer(IDisposable __state) => __state?.Dispose();

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.StartIssueWithAlternativeSolution))]
    [HarmonyPrefix]
    private static void AlternativeSolutionStartedPrefix(IssueBase __instance)
    {
        if (ModInformation.IsClient || !(__instance is Issue) ||
            !ContainerProvider.TryResolve<IIssueOwnerResolver>(out var owners) ||
            !owners.TryResolveOwnerHero(__instance, out var owner) || owner.PartyBelongedTo == null ||
            !ContainerProvider.TryResolve<IHeadmanHerdCleanup>(out var cleanup))
            return;

        cleanup.Remember(__instance, owner.PartyBelongedTo);
    }

    [HarmonyPatch(typeof(KillCharacterAction), nameof(KillCharacterAction.ApplyInternal))]
    [HarmonyPrefix]
    private static void GiverDeathStartedPrefix(Hero victim, out Issue __state)
    {
        __state = null;
        if (!ModInformation.IsServer || !victim.IsAlive || !(victim.Issue is Issue issue) || !issue.IsSolvingWithAlternative)
            return;

        pendingGiverDeaths.Add(issue);
        __state = issue;
    }

    [HarmonyPatch(typeof(KillCharacterAction), nameof(KillCharacterAction.ApplyInternal))]
    [HarmonyPostfix]
    private static void GiverDeathCompletedPostfix(Hero victim, Issue __state)
    {
        if (__state == null) return;
        if (victim.IsAlive)
        {
            pendingGiverDeaths.Remove(__state);
            return;
        }

        TryCancelCompanions(__state);
    }

    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.TryToMakeTroopsReturn))]
    [HarmonyPrefix]
    private static bool ReturnAlternativeTroopsPrefix(IssueBase issue)
    {
        if (ModInformation.IsClient || !(issue is Issue) ||
            !ContainerProvider.TryResolve<IHeadmanHerdCleanup>(out var cleanup))
            return true;

        return !cleanup.ReturnAlternativeTroops(issue);
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTick))]
    [HarmonyPostfix]
    private static void RetryCanceledCompanionsPostfix()
    {
        if (ModInformation.IsClient) return;
        var issues = pendingGiverDeaths
            .Concat(Campaign.Current.IssueManager.Issues.Values.OfType<Issue>()
                .Where(issue => issue.IsSolvingWithAlternative && !issue.IssueOwner.IsAlive))
            .Distinct()
            .ToList();
        foreach (var issue in issues)
            TryCancelCompanions(issue);
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAlternativeSolution))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool CancelInsteadOfCompletingPrefix(IssueBase __instance)
    {
        if (!(__instance is Issue issue) || issue.IssueOwner.IsAlive) return true;
        if (ModInformation.IsServer) TryCancelCompanions(issue);
        return false;
    }

    private static void TryCancelCompanions(Issue issue)
    {
        if (issue.IssueOwner.IsAlive)
        {
            pendingGiverDeaths.Remove(issue);
            return;
        }

        if (!ContainerProvider.TryResolve<IHeadmanHerdCleanup>(out var cleanup)) return;
        if (!cleanup.HasRememberedParty(issue))
        {
            pendingGiverDeaths.Remove(issue);
            return;
        }
        if (!ContainerProvider.TryResolve<IIssueOwnerResolver>(out var owners) ||
            !owners.TryResolveOwnerHero(issue, out var owner)) return;

        using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
        {
            if (!Campaign.Current.Models.IssueModel.CanTroopsReturnFromAlternativeSolution()) return;

            if (issue.IssueOwner.Issue == issue)
                Campaign.Current.IssueManager.OnHeroKilled(issue.IssueOwner, null, KillCharacterAction.KillCharacterActionDetail.None, false);
            else
                issue.CompleteIssueWithCancel();
        }

        pendingGiverDeaths.Remove(issue);
    }

    internal static void ForgetGiverDeath(Issue issue) => pendingGiverDeaths.Remove(issue);

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroKilled))]
    [HarmonyPostfix]
    private static void HeroKilledPostfix(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
    {
        RunAsOwners(quest => quest.OnHeroKilled(victim, killer, detail, showNotification));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnWarDeclared))]
    [HarmonyPostfix]
    private static void WarDeclaredPostfix(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
    {
        RunAsOwners(quest => quest.OnWarDeclared(faction1, faction2, declareWarDetail));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnClanChangedKingdom))]
    [HarmonyPostfix]
    private static void ClanChangedKingdomPostfix(Clan clan, Kingdom oldKingdom, Kingdom newKingdom,
        ChangeKingdomAction.ChangeKingdomActionDetail actionDetail, bool showNotification)
    {
        RunAsOwners(quest => quest.OnClanChangedKingdom(clan, oldKingdom, newKingdom, actionDetail, showNotification));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void MapEventStartedPostfix(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        if (mapEvent.IsForcingSupplies || mapEvent.IsForcingVolunteers)
            RunAsOwners(quest => quest.OnMapEventStarted(mapEvent, attackerParty, defenderParty));
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnHeroKilled))]
    [HarmonyPrefix]
    private static bool HeroKilledPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnWarDeclared))]
    [HarmonyPrefix]
    private static bool WarDeclaredPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnClanChangedKingdom))]
    [HarmonyPrefix]
    private static bool ClanChangedKingdomPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventStarted))]
    [HarmonyPrefix]
    private static bool MapEventStartedPrefix() => ModInformation.IsServer;

    private static void RunAsOwners(Action<Quest> listener)
    {
        if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)) dispatcher.RunAsOwners(listener);
    }
}
