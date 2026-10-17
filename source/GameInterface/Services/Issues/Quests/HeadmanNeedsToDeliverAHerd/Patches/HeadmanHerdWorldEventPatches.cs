using Common;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Heroes.Patches;
using HarmonyLib;
using System;
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
    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.OnHeroKilled))]
    [HarmonyPrefix]
    private static bool GiverKilledPrefix(Hero victim, out IDisposable __state)
    {
        __state = null;
        if (!(victim.Issue is Issue issue)) return true;
        if (ModInformation.IsClient) return false;
        if (issue.IssueQuest == null && !issue.IsSolvingWithAlternative) return true;
        if (!ContainerProvider.TryResolve<IIssueOwnerResolver>(out var owners) || !owners.TryResolveOwnerHero(issue, out var owner)) return false;
        __state = new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo);
        return true;
    }

    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.OnHeroKilled))]
    [HarmonyFinalizer]
    private static void GiverKilledFinalizer(IDisposable __state) => __state?.Dispose();

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
