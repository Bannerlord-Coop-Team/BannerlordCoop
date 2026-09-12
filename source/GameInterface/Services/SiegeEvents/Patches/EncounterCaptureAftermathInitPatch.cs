using Common;
using GameInterface.Services.SiegeEvents.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Backstop for a HOST attacker that captured a settlement but got stuck on the stale pre-mission siege
/// encounter menu ("The force besieging X has begun its assault!", dead Leave) instead of menu_settlement_taken.
/// The aftermath-choice prompt normally drives that transition, but a host attacker is still in its own mission
/// when the prompt arrives, so it parks in SiegeCaptureTransitionRetryHandler, and the deferred re-run can miss.
/// This fires off the observable stuck state — a siege-assault encounter for a fortification the local clan now
/// owns while the local party is outside it (the besieger, not the inside defender) — so it lands regardless of
/// why the prompt path failed. A winning defender is inside its town (and has PromptSiegeDefenderVictory), so it
/// never matches. Client-only; runs alongside EncounterAssaultInitGuardPatch on the same menu (disjoint: that one
/// gates the PlayerEncounter == null entry race).
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

        var settlement = PlayerEncounter.EncounterSettlement;
        if (settlement == null || !settlement.IsFortification) return true;

        var battle = PlayerEncounter.Battle;
        if (battle == null || !battle.IsSiegeAssault) return true;

        // Our clan captured it, and we are outside it (the besieger). A winning defender is inside its own town.
        if (settlement.OwnerClan == null || settlement.OwnerClan != Hero.MainHero?.Clan) return true;
        if (MobileParty.MainParty?.CurrentSettlement == settlement) return true;

        if (!ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface)) return true;

        siegeEventInterface.RouteCapturedSettlementToAftermathMenu(settlement);
        return false;
    }
}
