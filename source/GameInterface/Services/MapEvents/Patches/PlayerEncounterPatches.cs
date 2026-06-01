using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEvents.Messages.Leave;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(PlayerEncounter))]
internal class PlayerEncounterPatches
{
    [HarmonyPatch("StartBattleInternal")]
    [HarmonyPrefix]
    public static bool StartBattleInternalPrefix(ref PlayerEncounter __instance)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;

        return ModInformation.IsServer;
    }

    [HarmonyPatch("PlayerSurrenderInternal")]
    [HarmonyPrefix]
    public static bool PlayerSurrenderInternalPrefix(ref PlayerEncounter __instance)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;

        if (ModInformation.IsServer) return true;

        var message = new PlayerSurrendered(PlayerEncounter.Current._mapEvent, MobileParty.MainParty);

        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(PlayerEncounter.CheckNearbyPartiesToJoinPlayerMapEvent))]
    [HarmonyPrefix]
    private static bool PrefixCheckNearbyPartiesToJoinPlayerMapEvent()
    {
        return false;
    }
}
