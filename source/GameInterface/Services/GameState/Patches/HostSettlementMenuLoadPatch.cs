using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;

namespace GameInterface.Services.GameState.Patches;

[HarmonyPatch]
internal class HostSettlementMenuLoadPatch
{
    [HarmonyPatch(typeof(MapState), nameof(MapState.OnLoadingFinished))]
    [HarmonyPostfix]
    static void ExitStaleSettlementMenu(MapState __instance)
    {
        // The host must never sit in a settlement/game menu. A save made while the hero was inside
        // one restores that menu here on load; leaving it now, before anything can transfer the
        // host's live state to a joining client, keeps the stale menu from being baked into that snapshot.
        // RemoveMainParty has already run by this point (it fires earlier in the same load, from
        // CampaignReady), so MobileParty.MainParty is gone; PlayerEncounter.Finish() assumes a live
        // main party and would NRE, so the encounter is cleared directly instead.
        if (ModInformation.IsClient || !__instance.AtMenu)
            return;

        __instance.ExitMenuMode();
        Campaign.Current.PlayerEncounter = null;
        Campaign.Current.LocationEncounter = null;
    }
}
