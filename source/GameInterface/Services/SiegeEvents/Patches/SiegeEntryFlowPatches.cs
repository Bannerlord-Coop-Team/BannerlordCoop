using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using Helpers;
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

        // The host must run native directly: BreakSiegeAttempted only has a handler in the client
        // container, and the running server's sync policy never allows the original, so without this
        // bail the host's own leave click published into the void and its camp never cleared.
        if (ModInformation.IsServer) return true;

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

    // A besieger's encounter-menu "Leave..." (the menu a player lands on after retreating out of a siege
    // battle) funnels into MenuHelper.EncounterLeaveConsequence, which finishes the encounter and clears
    // MainParty.BesiegerCamp directly — a client-local write the server never sees, so the party stayed in
    // the camp and the siege map event there (issue #2263). Route the camp write through the server; the
    // approval runs the local encounter finish. Patching the MenuHelper funnel (not the menu consequence)
    // also keeps vanilla's "abandon the siege?" confirmation, whose Yes callback lands here too.
    [HarmonyPatch(typeof(MenuHelper), nameof(MenuHelper.EncounterLeaveConsequence))]
    [HarmonyPrefix]
    private static bool EncounterLeaveConsequencePrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;
        if (MobileParty.MainParty.BesiegerCamp == null) return true;

        MessageBroker.Instance.Publish(null, new BreakSiegeAttempted(MobileParty.MainParty));
        return false;
    }

    // The post-battle "Leave the siege" option (continue_siege_after_attack menu) writes the camp directly
    // instead of calling LeaveSiege, so the LeaveSiege reroute never fires for it. Same split as above:
    // the server owns the camp write, the approval closes the menu.
    [HarmonyPatch(typeof(EncounterGameMenuBehavior), nameof(EncounterGameMenuBehavior.leave_siege_after_attack_on_consequence))]
    [HarmonyPrefix]
    private static bool LeaveSiegeAfterAttackPrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;
        if (MobileParty.MainParty.BesiegerCamp == null) return true;

        MessageBroker.Instance.Publish(null, new BreakSiegeAttempted(MobileParty.MainParty));
        return false;
    }
}
