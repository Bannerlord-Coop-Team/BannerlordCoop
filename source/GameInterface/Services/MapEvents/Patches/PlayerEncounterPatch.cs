using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Start;
using HarmonyLib;
using Helpers;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Patches the StartBattle in PlayerEncounter, only runs on local client
/// </summary>
[HarmonyPatch(typeof(PlayerEncounter))]
public class PlayerEncounterPatch
{
    [HarmonyPatch("StartBattleInternal")]
    [HarmonyPrefix]
    public static bool StartBattleInternalPrefix(ref PlayerEncounter __instance)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;

        if (ModInformation.IsServer) return true;

        return false;
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
}

[HarmonyPatch(typeof(MenuHelper))]
public class MenuHelperPatches
{
    [HarmonyPatch(nameof(MenuHelper.EncounterLeaveConsequence))]
    [HarmonyPrefix]
    public static bool Prefix()
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;

        if (ModInformation.IsServer) return true;

        MapEvent mapEvent = PlayerEncounter.Battle ?? PlayerEncounter.EncounteredBattle;
        var message = new PlayerLeaveBattle(mapEvent, MobileParty.MainParty);

        MessageBroker.Instance.Publish(null, message);

        return false;
    }
}