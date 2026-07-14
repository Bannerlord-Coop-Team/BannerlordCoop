using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Routes the player's siege entry and exit menu actions through the server. The world-authoritative
/// part (SiegeEventManager.StartSiegeEvent, the MobileParty.BesiegerCamp writes) runs server side with
/// patches live; the requester runs only the player-local menu continuation when the approval arrives.
/// </summary>
[HarmonyPatch]
internal class SiegeEntryFlowPatches
{
    [HarmonyPatch(typeof(EncounterGameMenuBehavior), nameof(EncounterGameMenuBehavior.game_menu_town_town_besiege_on_consequence))]
    [HarmonyPrefix]
    private static bool BesiegeConsequencePrefix()
    {
        if (ModInformation.IsServer) return true;

        MessageBroker.Instance.Publish(null, new BesiegeSettlementAttempted(MobileParty.MainParty, Settlement.CurrentSettlement));
        return false;
    }

    [HarmonyPatch(typeof(EncounterGameMenuBehavior), nameof(EncounterGameMenuBehavior.game_menu_join_siege_event_on_consequence))]
    [HarmonyPrefix]
    private static bool JoinSiegeConsequencePrefix()
    {
        if (ModInformation.IsServer) return true;

        // With a live assault the vanilla branch joins the MapEvent, which the existing map-event
        // join intercepts already round-trip; only the camp join during preparation needs routing.
        if (Settlement.CurrentSettlement?.Party?.MapEvent != null) return true;

        MessageBroker.Instance.Publish(null, new JoinSiegeCampAttempted(MobileParty.MainParty, Settlement.CurrentSettlement));
        return false;
    }

    [HarmonyPatch(typeof(SiegeEventCampaignBehavior), nameof(SiegeEventCampaignBehavior.menu_siege_leave_on_consequence))]
    [HarmonyPrefix]
    private static bool SettlementDefenderLeavePrefix()
    {
        if (ModInformation.IsServer) return true;

        var mainParty = MobileParty.MainParty;
        if (mainParty.BesiegerCamp != null || mainParty.CurrentSettlement == null)
            return true;

        var army = mainParty.Army;
        if (army != null)
        {
            MessageBroker.Instance.Publish(army, new MobilePartyInArmyRemoved(army, mainParty, mainParty));
            using (new AllowedThread())
            {
                ArmyPatches.RemoveMobilePartyInArmy(mainParty, army, mainParty);
            }
        }
        PlayerSiege.FinalizePlayerSiege();

        return PlayerLeaveSettlementPatch.RequestLeave();
    }

    // The besieger leader's "Lead an assault" (and the follower "Send troops" order) locally start an encounter
    // and the Siege MapEvent, which the client can't create. Route it to the server, which runs
    // ApplyStartAssaultAgainstWalls authoritatively; the besieger's encounter is established by the resulting
    // NetworkPromptSiegeAssault, not here.
    [HarmonyPatch(typeof(SiegeEventCampaignBehavior), nameof(SiegeEventCampaignBehavior.menu_siege_strategies_lead_assault_on_consequence))]
    [HarmonyPrefix]
    private static bool LeadAssaultConsequencePrefix()
    {
        if (ModInformation.IsServer) return true;

        MessageBroker.Instance.Publish(null, new AssaultSiegeAttempted(MobileParty.MainParty, MobileParty.MainParty.BesiegedSettlement));
        return false;
    }

    [HarmonyPatch(typeof(SiegeEventCampaignBehavior), nameof(SiegeEventCampaignBehavior.menu_order_an_assault_on_consequence))]
    [HarmonyPrefix]
    private static bool OrderAssaultConsequencePrefix()
    {
        if (ModInformation.IsServer) return true;

        MessageBroker.Instance.Publish(null, new AssaultSiegeAttempted(MobileParty.MainParty, MobileParty.MainParty.BesiegedSettlement));
        return false;
    }

    [HarmonyPatch(typeof(SiegeEventCampaignBehavior), nameof(SiegeEventCampaignBehavior.LeaveSiege))]
    [HarmonyPrefix]
    private static bool LeaveSiegePrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        MessageBroker.Instance.Publish(null, new BreakSiegeAttempted(MobileParty.MainParty));
        return false;
    }

    // The army follower "Leave Army" option clears the camp and the army in one consequence. The army
    // write already flows through the co-op army patches, so only the camp write is rerouted.
    [HarmonyPatch(typeof(SiegeEventCampaignBehavior), nameof(SiegeEventCampaignBehavior.menu_siege_strategies_passive_wait_leave_on_consequence))]
    [HarmonyPrefix]
    private static bool PassiveWaitLeavePrefix()
    {
        if (ModInformation.IsServer) return true;

        GameMenu.ExitToLast();
        if (PlayerSiege.PlayerSiegeEvent != null)
        {
            PlayerSiege.FinalizePlayerSiege();
        }

        MessageBroker.Instance.Publish(null, new BreakSiegeAttempted(MobileParty.MainParty));
        MobileParty.MainParty.Army = null;
        return false;
    }
}
