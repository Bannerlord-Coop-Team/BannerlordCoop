using Common;
using HarmonyLib;
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
    [HarmonyPatch(typeof(Quest), nameof(Quest.CreateDeserterParty))]
    [HarmonyPatch(typeof(Quest), nameof(Quest.CreateDefenderParty))]
    [HarmonyPatch(typeof(Quest), nameof(Quest.DestroyDeserterParty))]
    [HarmonyPatch(typeof(Quest), nameof(Quest.DestroyDefenderParty))]
    [HarmonyPatch(typeof(Quest), nameof(Quest.ReleaseDeserterParty))]
    [HarmonyPrefix]
    private static bool WorldPartyPrefix() => ModInformation.IsServer;

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

    [HarmonyPatch(typeof(Quest), nameof(Quest.HourlyTick))]
    [HarmonyPatch(typeof(Quest), nameof(Quest.GameMenuOptionSelected))]
    [HarmonyPrefix]
    private static bool TickPrefix() => ModInformation.IsServer;

    private static void Request(Quest quest, ExtortionByDesertersBranch branch)
    {
        if (ContainerProvider.TryResolve<Framework.Interface.IOutcomeDispatcher>(out var dispatcher))
        {
            dispatcher.BeforeQuestBranch(quest, (byte)branch);
        }
    }
}
