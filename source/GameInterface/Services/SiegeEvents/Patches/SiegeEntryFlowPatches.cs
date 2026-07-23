using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using Helpers;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
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

    // The join_siege_event menu's "Don't get involved." drops an army follower out of its army, removes the
    // party from any battle it is on a side of, clears the camp, then holds the party. Suppress-and-route like
    // the leave menus above; the army exit and the battle-side removal each replicate through their own co-op
    // flow (neither MobileParty.Army nor a single-party MapEventSide removal is auto-synced), the camp write
    // routes through the server, and the hold is reissued here (see HoldAfterSiegeLeave).
    [HarmonyPatch(typeof(EncounterGameMenuBehavior), nameof(EncounterGameMenuBehavior.break_in_leave_consequence))]
    [HarmonyPrefix]
    private static bool BreakInLeavePrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;

        var mainParty = MobileParty.MainParty;
        if (mainParty.BesiegerCamp == null) return true;

        var army = mainParty.Army;
        if (army != null && army.LeaderParty != mainParty)
        {
            MessageBroker.Instance.Publish(army, new MobilePartyInArmyRemoved(army, mainParty, mainParty));
            using (new AllowedThread())
            {
                ArmyPatches.RemoveMobilePartyInArmy(mainParty, army, mainParty);
            }
        }

        // A besieger prompted into join_siege_event can already sit on a side of the sally-out/relief map
        // event; native removes it there before clearing the camp. That single-party removal is not
        // auto-synced, so route it or the party stays in that battle on the server.
        if (mainParty.Party.MapEventSide != null)
        {
            MessageBroker.Instance.Publish(mainParty, new PlayerLeaveBattleAttempted(mainParty.Party));
        }

        MessageBroker.Instance.Publish(null, new BreakSiegeAttempted(mainParty));
        HoldAfterSiegeLeave(mainParty);
        return false;
    }

    // Try-to-get-away accept: the camp write sits between the troop/item sacrifice and the debrief menu,
    // all of which must keep running locally, so the native body cannot be suppressed. Route the camp
    // removal and clear it locally up front under AllowedThread (applied without forwarding or the
    // client-write error log); the native guarded write then sees null and skips itself. The approval
    // must not touch the menus — the native flow continues into the debrief on its own.
    [HarmonyPatch(typeof(EncounterGameMenuBehavior), nameof(EncounterGameMenuBehavior.game_menu_encounter_leave_your_soldiers_behind_accept_on_consequence))]
    [HarmonyPrefix]
    private static bool LeaveSoldiersBehindAcceptPrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;
        if (MobileParty.MainParty.BesiegerCamp == null) return true;

        MessageBroker.Instance.Publish(null, new BreakSiegeAttempted(MobileParty.MainParty, finishLocalMenus: false));
        using (new AllowedThread())
        {
            MobileParty.MainParty.BesiegerCamp = null;
        }
        return true;
    }

    // A besieger defeated in a real-time mission unwinds through the native defeat state machine
    // (PlayerEncounter.Update still sees a live PlayerMapEvent; the coop battle-results path only forces
    // the encounter state once the server's commit replicates), and DoPlayerDefeat clears the camp
    // client-locally on that path. Neither the captivity flow nor the field-battle finalize converges the
    // camp on the server, so route it; native must keep running for Finish and the taken-prisoner menu.
    // Pre-nulling also drops the stale continue_siege_after_attack branch inside Finish (it keys on
    // BesiegedSettlement), which native would otherwise show for a retreat-ended defeat with the camp
    // already gone.
    [HarmonyPatch(typeof(PlayerEncounter), nameof(PlayerEncounter.DoPlayerDefeat))]
    [HarmonyPrefix]
    private static bool PlayerDefeatPrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;
        if (MobileParty.MainParty.BesiegerCamp == null) return true;

        MessageBroker.Instance.Publish(null, new BreakSiegeAttempted(MobileParty.MainParty, finishLocalMenus: false));
        using (new AllowedThread())
        {
            MobileParty.MainParty.BesiegerCamp = null;
        }
        return true;
    }

    // Safe-passage barter: accepting "let me go" while besieging abandons the siege via a camp write
    // buried mid-Apply. The native body must keep running (AI holds, LeaveEncounter). Branch A — the
    // besieger GRANTING passage to sallying defenders — keeps the siege, so it must never route a break;
    // it is re-evaluated here, before the pre-null, because it reads OriginalParty.SiegeEvent, which the
    // pre-null would clear when the original party is the main party.
    [HarmonyPatch(typeof(SafePassageBarterable), nameof(SafePassageBarterable.Apply))]
    [HarmonyPrefix]
    private static bool SafePassageApplyPrefix(SafePassageBarterable __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;
        if (PlayerEncounter.Current == null) return true;
        if (MobileParty.MainParty.BesiegerCamp == null) return true;

        var originalParty = __instance.OriginalParty;
        if (originalParty?.SiegeEvent != null
            && originalParty.SiegeEvent.BesiegerCamp.HasInvolvedPartyForEventType(originalParty)
            && __instance._otherParty != null
            && originalParty.SiegeEvent.BesiegedSettlement.HasInvolvedPartyForEventType(__instance._otherParty))
            return true;

        MessageBroker.Instance.Publish(null, new BreakSiegeAttempted(MobileParty.MainParty, finishLocalMenus: false));
        using (new AllowedThread())
        {
            MobileParty.MainParty.BesiegerCamp = null;
        }
        return true;
    }

    // break_in_leave_consequence and the join_encounter leave lambda both call MobileParty.SetMoveModeHold
    // after PlayerEncounter.Finish (the other routed leave menus do not). The approval runs Finish under an
    // AllowedThread, so PlayerEncounterPatches.FinishPostfix short-circuits and never reissues the hold. Do it
    // here: hold locally, then publish through the gated AI-behavior channel so the server clears the party's
    // stale besiege order and re-broadcasts the hold to every client — a bare local write is dropped by dynamic
    // sync, leaving the party moving under its old order on the server (same reasoning as FinishPostfix).
    internal static void HoldAfterSiegeLeave(MobileParty party)
    {
        party.SetMoveModeHold();
        MessageBroker.Instance.Publish(party.Ai, new PartyBehaviorChangeAttempted(party));
    }
}

/// <summary>
/// The join_encounter menu's leave option is a compiler-generated lambda in
/// <see cref="EncounterGameMenuBehavior.AddGameMenus"/> that clears <see cref="MobileParty.BesiegerCamp"/>,
/// finishes the encounter, then holds the party, so it needs IL-based target resolution instead of a name. It
/// is the only lambda in that behavior touching the camp setter. Camp write routes through the server; the
/// hold is reissued locally (see <see cref="SiegeEntryFlowPatches.HoldAfterSiegeLeave"/>).
/// </summary>
[HarmonyPatch]
internal class JoinEncounterLeaveLambdaPatches
{
    private static MethodBase joinEncounterLeaveConsequence;

    internal static MethodBase ResolveJoinEncounterLeaveConsequence()
    {
        if (joinEncounterLeaveConsequence != null) return joinEncounterLeaveConsequence;

        var campSetter = AccessTools.PropertySetter(typeof(MobileParty), nameof(MobileParty.BesiegerCamp));

        foreach (var nested in typeof(EncounterGameMenuBehavior).GetNestedTypes(AccessTools.all))
        {
            foreach (var method in AccessTools.GetDeclaredMethods(nested))
            {
                if (!method.Name.Contains(nameof(EncounterGameMenuBehavior.AddGameMenus))) continue;
                if (method.GetMethodBody() == null) continue;

                if (PatchProcessor.ReadMethodBody(method).Any(op => Equals(op.Value, campSetter)))
                {
                    joinEncounterLeaveConsequence = method;
                    return method;
                }
            }
        }

        return null;
    }

    private static MethodBase TargetMethod() => ResolveJoinEncounterLeaveConsequence();

    [HarmonyPrefix]
    private static bool JoinEncounterLeavePrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;

        var mainParty = MobileParty.MainParty;
        if (mainParty.BesiegerCamp == null) return true;

        MessageBroker.Instance.Publish(null, new BreakSiegeAttempted(mainParty));
        SiegeEntryFlowPatches.HoldAfterSiegeLeave(mainParty);
        return false;
    }
}
