using Common;
using GameInterface.Services.SiegeEvents.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Backstop for an attacker that captured a settlement but got stuck on a dead encounter menu instead of
/// menu_settlement_taken. The aftermath-choice prompt normally drives that transition, but it can be missed:
/// a host attacker is still in its own mission when the prompt arrives so it parks in
/// SiegeCaptureTransitionRetryHandler and the deferred re-run can miss, and a capturing client can have the
/// menu re-driven out from under it by a late settlement-encounter approval.
///
/// This fires off the observable stuck state — a capture for a fortification the local clan now owns, with the
/// siege already gone and the local party outside it — so it lands regardless of why the prompt path failed.
/// A winning defender is inside its town (and has PromptSiegeDefenderVictory), so it never matches.
///
/// Covers both capture shapes vanilla routes to menu_settlement_taken: a siege assault won as the attacker,
/// and a sally-out repelled by the besieger, which captures the town outright. Client-only; runs alongside
/// EncounterAssaultInitGuardPatch on the same menu (disjoint: that one gates the PlayerEncounter == null race).
/// </summary>
[HarmonyPatch(typeof(EncounterGameMenuBehavior))]
internal class EncounterCaptureAftermathInitPatch
{
    [HarmonyPatch("game_menu_encounter_on_init")]
    [HarmonyPrefix]
    private static bool Prefix()
    {
        if (ModInformation.IsServer) return true;
        if (PlayerEncounter.Current == null) return true;

        if (!IsStrandedCaptureEncounter(
                PlayerEncounter.Battle,
                PlayerEncounter.EncounterSettlement,
                Hero.MainHero?.Clan,
                MobileParty.MainParty))
            return true;

        if (!ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface)) return true;

        siegeEventInterface.RouteCapturedSettlementToAftermathMenu(PlayerEncounter.EncounterSettlement);
        return false;
    }

    /// <summary>
    /// True for the observable stuck state: a capture battle for a fortification the local clan now owns while
    /// the local party is outside it.
    /// </summary>
    /// <remarks>
    /// Vanilla PlayerEncounter.DoEnd routes two winning-besieger shapes to menu_settlement_taken: a siege assault
    /// won as the attacker (IL_0142-017f), and a sally-out won as the defender - the besieger repelling the
    /// sortie, which in vanilla captures the town outright because the whole garrison committed and lost
    /// (IL_0180-01fc). Co-op never calls DoEnd, so this backstop has to cover both; it previously demanded
    /// IsSiegeAssault, so a sortie capture had no backstop at all.
    ///
    /// A winning defender is inside its own town and has PromptSiegeDefenderVictory, so it never matches.
    /// </remarks>
    internal static bool IsStrandedCaptureEncounter(
        MapEvent battle, Settlement settlement, Clan playerClan, MobileParty mainParty)
    {
        if (settlement == null || !settlement.IsFortification) return false;
        if (playerClan == null || mainParty == null) return false;

        // We are outside it: the besieger, not the inside defender.
        if (mainParty.CurrentSettlement == settlement) return false;

        // The capture ended the siege (KingdomManager.SiegeCompleted -> RemoveAllSiegeParties), so a live
        // siege means this is not a capture aftermath. This supplies the specificity the old IsSiegeAssault
        // gate used to, and keeps the patch off every live-siege menu.
        if (settlement.SiegeEvent != null || mainParty.BesiegerCamp != null) return false;

        // A sally-out capture tears its map event down before this menu opens, so PlayerEncounter.Battle
        // (== Current._mapEvent) is null by then - the live capture of the stuck state showed exactly that.
        // Requiring a battle here is what made this backstop dead for the case it most needs to cover, so
        // fall back to the pending aftermath choice, which the server does prompt for a sortie capture.
        if (SiegeCaptureMenuHoldPatch.IsHeld(settlement)) return true;

        if (battle == null) return false;

        // TODO: IsBlockadeSallyOut is included for completeness but is untested - the naval sally-out only
        // occurs with the War Sails DLC, which this mod does not support (see README, "Do not enable the
        // War Sails DLC"). Revisit alongside blockade support.
        if (!battle.IsSiegeAssault && !battle.IsSallyOut && !battle.IsBlockadeSallyOut) return false;

        // ChangeOwnerOfSettlementAction.ApplyBySiege gives OwnerClan to the KINGDOM LEADER and records the
        // actual capturer in Town.LastCapturedBy, so an OwnerClan-only test fails any capturer who is not
        // their own ruler.
        return settlement.OwnerClan == playerClan || settlement.Town?.LastCapturedBy == playerClan;
    }
}
