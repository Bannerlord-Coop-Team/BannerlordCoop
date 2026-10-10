using Common;
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
/// instead of menu_settlement_taken, for example when the map rebuilds that menu after the post-battle loot screen
/// with no live encounter left. Routes off the observable stuck state, so it lands regardless of why the prompt path missed.
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
        if (!TryGetCapturedSettlement(out var settlement)) return true;
        if (!ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface)) return true;

        siegeEventInterface.RouteCapturedSettlementToAftermathMenu(settlement);
        return false;
    }

    private static bool TryGetCapturedSettlement(out Settlement settlement)
    {
        // The leader's held capture choice; also covers a vassal, whose fief stays with the ruler until the claimant vote.
        if (SiegeCaptureMenuHoldPatch.TryGetHeldSettlement(PlayerEncounter.Current, out settlement)) return true;
        if (PlayerEncounter.Current == null) return false;

        settlement = PlayerEncounter.EncounterSettlement;
        if (settlement == null || !settlement.IsFortification) return false;

        var battle = PlayerEncounter.Battle;
        if (battle == null || !battle.IsSiegeAssault) return false;

        // Our clan captured it, and we are outside it (the besieger). A winning defender is inside its own town.
        if (settlement.OwnerClan == null || settlement.OwnerClan != Hero.MainHero?.Clan) return false;
        return MobileParty.MainParty?.CurrentSettlement != settlement;
    }
}
