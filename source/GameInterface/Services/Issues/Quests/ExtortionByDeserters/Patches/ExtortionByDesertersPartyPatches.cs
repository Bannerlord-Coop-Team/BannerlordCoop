using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters.Patches;

/// <summary>World creation and cleanup belong to the server, encounters to the owning client.</summary>
[HarmonyPatch]
internal class ExtortionByDesertersPartyPatches
{
    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddTrackedObject))]
    [HarmonyPrefix]
    private static bool AddTrackedObjectPrefix(QuestBase __instance, ITrackableCampaignObject trackedObject)
    {
        // Its constructor runs before the server's party binding arrives.
        return !(__instance is Quest) || trackedObject != null;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.StartAmbushEncounter))]
    [HarmonyPrefix]
    private static bool StartAmbushEncounterPrefix(Quest __instance) =>
        ModInformation.IsClient && __instance._deserterMobileParty != null && __instance._defenderMobileParty != null;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnBattleStarted))]
    [HarmonyPrefix]
    private static bool OnBattleStartedPrefix(Quest __instance, PartyBase attackerParty)
    {
        if (ModInformation.IsClient && attackerParty == __instance._deserterMobileParty?.Party)
        {
            Request(__instance, ExtortionByDesertersBranch.Ambush);
        }
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.GameMenuOpened))]
    [HarmonyPrefix]
    private static bool GameMenuOpenedPrefix(Quest __instance, MenuCallbackArgs mArgs)
    {
        if (ModInformation.IsClient && mArgs.MenuContext.GameMenu.StringId == "encounter")
        {
            Request(__instance, ExtortionByDesertersBranch.BattleCleanup);
        }
        return false;
    }

    private static void Request(Quest quest, ExtortionByDesertersBranch branch)
    {
        if (ContainerProvider.TryResolve<Framework.Interface.IOutcomeDispatcher>(out var dispatcher))
        {
            dispatcher.BeforeQuestBranch(quest, (byte)branch);
        }
    }
}

/// <summary>These world methods must stay server-only even during client replay.</summary>
[HarmonyPatch]
internal class ExtortionByDesertersServerMethodsPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.CreateDeserterParty));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.CreateDefenderParty));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.DestroyDeserterParty));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.DestroyDefenderParty));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.ReleaseDeserterParty));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.HourlyTick));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.GameMenuOptionSelected));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnWarDeclared));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnClanChangedKingdom));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnMapEventStarted));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnSettlementLeft));
    }

    [HarmonyPrefix]
    private static bool Prefix() => ModInformation.IsServer;
}
