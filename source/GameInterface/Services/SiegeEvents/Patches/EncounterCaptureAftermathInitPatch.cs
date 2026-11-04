using Common;
using GameInterface.Services.SiegeEvents.Handlers;
using GameInterface.Services.SiegeEvents.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Backstop for a capturing leader that lands back on the stale pre-mission siege encounter menu (dead Leave)
/// instead of menu_settlement_taken once the battle has resolved, e.g. its encounter End was skipped or the prompt
/// arrived late. Routes off the observable stuck state, so it lands regardless of why the normal paths missed.
/// Client-only; runs alongside EncounterAssaultInitGuardPatch on the same menu.
/// </summary>
[HarmonyPatch(typeof(EncounterGameMenuBehavior))]
internal class EncounterCaptureAftermathInitPatch
{
    [HarmonyPatch("game_menu_encounter_on_init")]
    [HarmonyPrefix]
    internal static bool Prefix()
    {
        if (ModInformation.IsServer) return true;
        // The presentation and loot steps still own this menu; the encounter End routes the leader after them.
        if (SiegeCaptureTransitionRetryHandler.IsBattleStillResolving()) return true;
        if (!TryGetCapturedSettlement(out var settlement)) return true;
        if (!ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface)) return true;

        siegeEventInterface.RouteCapturedSettlementToAftermathMenu(settlement);
        return false;
    }

    private static bool TryGetCapturedSettlement(out Settlement settlement)
    {
        settlement = null;
        if (PlayerEncounter.Current == null)
        {
            // Finished stale encounter: the leader's held capture choice, which also covers a vassal whose fief stays
            // with the ruler until the claimant vote. A live map event (a new attack) owns the menu instead.
            return MobileParty.MainParty?.MapEvent == null && SiegeCaptureMenuHoldPatch.TryGetOnlyHeldSettlement(out settlement);
        }

        settlement = PlayerEncounter.EncounterSettlement;
        if (settlement == null || !settlement.IsFortification) return false;
        if (MobileParty.MainParty?.MapEvent == null && SiegeCaptureMenuHoldPatch.IsHeld(settlement)) return true;

        var battle = PlayerEncounter.Battle;
        if (battle == null || !battle.IsSiegeAssault) return false;

        // Our clan captured it, and we are outside it (the besieger). A winning defender is inside its own town.
        if (settlement.OwnerClan == null || settlement.OwnerClan != Hero.MainHero?.Clan) return false;
        return MobileParty.MainParty?.CurrentSettlement != settlement;
    }
}
