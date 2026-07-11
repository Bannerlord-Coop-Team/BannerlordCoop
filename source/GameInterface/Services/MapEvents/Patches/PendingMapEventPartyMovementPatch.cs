using GameInterface.Services.MapEvents.Initialization;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.TryToMoveThePartyWithCurrentTickMoveData))]
internal static class PendingMapEventPartyMovementPatch
{
    [HarmonyPrefix]
    private static bool Prefix(MobileParty __instance) => CanAdvancePosition(__instance?.Party);

    internal static bool CanAdvancePosition(PartyBase party) =>
        !PendingMapEventPartyLock.IsLocked(party);
}
