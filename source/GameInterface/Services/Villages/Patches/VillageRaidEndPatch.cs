using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.MapEvents.Messages.Leave;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch]
internal class VillageRaidEndPatch
{
    [HarmonyPatch(typeof(VillageHostileActionCampaignBehavior), nameof(VillageHostileActionCampaignBehavior.wait_menu_end_raiding_on_consequence))]
    [HarmonyPrefix]
    private static bool Prefixwait_menu_end_raiding_on_consequence()
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
    [HarmonyPatch(typeof(VillageHostileActionCampaignBehavior), nameof(VillageHostileActionCampaignBehavior.wait_menu_end_raiding_at_army_by_leaving_on_consequence))]
    [HarmonyPrefix]
    private static bool Prefixwait_menu_end_raiding_at_army_by_leaving_on_consequence()
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

        var message = new MobilePartyInArmyRemoved(MobileParty.MainParty.Army, MobileParty.MainParty, MobileParty.MainParty);
        MessageBroker.Instance.Publish(null, message);
        return false;
    }
    [HarmonyPatch(typeof(VillageHostileActionCampaignBehavior), nameof(VillageHostileActionCampaignBehavior.wait_menu_end_raiding_at_army_by_abandoning_on_consequence))]
    [HarmonyPrefix]
    private static bool Prefixwait_menu_end_raiding_at_army_by_abandoning_on_consequence()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;

        MessageBroker.Instance.Publish(null,
            new ChangeClanInfluence(Clan.PlayerClan,
            (int)(float)Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfAbandoningArmy()));

        var mapEvent = PlayerEncounter.Battle ?? MapEvent.PlayerMapEvent;
        if (mapEvent != null)
        {
            MessageBroker.Instance.Publish(mapEvent, new MapEventFinalizeAttempted(mapEvent));
            return false;
        }

        CloseLocalRaidMenu();

        var message = new MobilePartyInArmyRemoved(MobileParty.MainParty.Army, MobileParty.MainParty, MobileParty.MainParty);
        MessageBroker.Instance.Publish(null, message);
        return false;
    }

    private static void CloseLocalRaidMenu()
    {
        if (PlayerEncounter.Current != null)
            PlayerEncounter.Finish(true);

        GameMenu.ExitToLast();
    }
}