using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Messages.Leave;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch]
internal class VillageRaidEndPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(VillageHostileActionCampaignBehavior), "wait_menu_end_raiding_on_consequence");
        yield return AccessTools.Method(typeof(VillageHostileActionCampaignBehavior), "wait_menu_end_raiding_at_army_by_leaving_on_consequence");
        yield return AccessTools.Method(typeof(VillageHostileActionCampaignBehavior), "wait_menu_end_raiding_at_army_by_abandoning_on_consequence");
    }

    [HarmonyPrefix]
    private static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;

        var mapEvent = PlayerEncounter.Battle ?? MapEvent.PlayerMapEvent;
        if (mapEvent != null)
        {
            MessageBroker.Instance.Publish(mapEvent, new MapEventFinalizeAttempted(mapEvent));
            return false;
        }

        CloseLocalRaidMenu();
        return false;
    }

    private static void CloseLocalRaidMenu()
    {
        if (PlayerEncounter.Current != null)
            PlayerEncounter.Finish(true);

        GameMenu.ExitToLast();
    }
}