using Common;
using Common.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Patches;

using Quest = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

[HarmonyPatch(typeof(Quest))]
internal class ExtortionQuestWorldPatches
{
    [HarmonyPatch(nameof(Quest.OnMapEventStarted))]
    [HarmonyPrefix]
    private static bool MapEventStartedPrefix(Quest __instance, MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<IExtortionQuestWorld>(out var world))
            world.BattleStarted(__instance, mapEvent, attackerParty, defenderParty);
        return false;
    }

    [HarmonyPatch(nameof(Quest.OnSettlementLeft))]
    [HarmonyPrefix]
    private static bool SettlementLeftPrefix(Quest __instance) => __instance._deserterMobileParty != null;

    [HarmonyPatch(nameof(Quest.StartAmbushEncounter))]
    [HarmonyPrefix]
    private static bool StartAmbushPrefix(Quest __instance)
    {
        if (ModInformation.IsClient)
            return ExtortionQuestMirrorScope.IsActive && AllowedThread.IsThisThreadAllowed();
        if (ContainerProvider.TryResolve<IExtortionQuestWorld>(out var world)) world.StartAmbush(__instance);
        return false;
    }

    [HarmonyPatch(nameof(Quest.GameMenuOpened))]
    [HarmonyPrefix]
    private static bool GameMenuOpenedPrefix() => false;

    [HarmonyPatch(nameof(Quest.TickDesertersPartyLogic))]
    [HarmonyPrefix]
    private static bool TickPrefix(Quest __instance)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<IExtortionQuestWorld>(out var world))
            world.Tick(__instance);
        return false;
    }

    [HarmonyPatch(nameof(Quest.OnVillageBeingRaided))]
    [HarmonyPrefix]
    private static bool VillageRaidedPrefix(Quest __instance, Village village)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<IExtortionQuestWorld>(out var world))
            world.VillageRaided(__instance, village);
        return false;
    }
}
