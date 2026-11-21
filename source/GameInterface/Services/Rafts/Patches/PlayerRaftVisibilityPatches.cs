using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using SandBox.ViewModelCollection.Nameplate;
using TaleWorlds.CampaignSystem.Party;
using static TaleWorlds.CampaignSystem.Party.MobileParty;

namespace GameInterface.Services.Rafts.Patches;

/// <summary>
/// Other players' rafts and ferries stay visible with their nameplate but can't be engaged, so a player sailing back
/// after escaping captivity or crossing on a ferry can be seen without being attacked or talked to.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class PlayerRaftVisibilityPatches
{
    // Remembers whether the camera left the nameplate visible before vanilla's hide rules run
    [HarmonyPatch(typeof(PartyNameplateVM), nameof(PartyNameplateVM.RefreshDynamicProperties))]
    [HarmonyPrefix]
    private static void RefreshDynamicPropertiesPrefix(PartyNameplateVM __instance, out bool __state) => __state = __instance._isVisibleOnMapBind;

    // Vanilla hides the nameplate of every non-main party in naval auto travel (a raft or a ferry), so show other players' rafts and ferries again
    [HarmonyPatch(typeof(PartyNameplateVM), nameof(PartyNameplateVM.RefreshDynamicProperties))]
    [HarmonyPostfix]
    private static void RefreshDynamicPropertiesPostfix(PartyNameplateVM __instance, bool __state)
    {
        var party = __instance.Party;
        // Vanilla's army and settlement hides still apply to a raft or ferry, and so does a disconnected player's parked party
        var hiddenOnlyForNavalAutoTravel = __state && !__instance._isVisibleOnMapBind &&
            !__instance._isInArmyBind && !__instance._isInSettlementBind && party.IsInNavalAutoTravel && party.IsActive;

        if (!hiddenOnlyForNavalAutoTravel) return;
        if (!IsOtherPlayersRaftOrFerry(party, MobileParty.MainParty)) return;

        __instance._isVisibleOnMapBind = true;
    }

    [HarmonyPatch(typeof(PartyBase), "TaleWorlds.CampaignSystem.Map.IInteractablePoint.CanPartyInteract")]
    [HarmonyPostfix]
    private static void CanPartyInteractPostfix(PartyBase __instance, MobileParty mobileParty, ref bool __result)
    {
        if (!__result) return;

        if (IsOtherPlayersRaftOrFerry(__instance.MobileParty, mobileParty))
            __result = false;
    }

    // A click on another player's raft or ferry moves next to it instead of following it forever
    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.SetMoveEngageParty))]
    [HarmonyPrefix]
    private static bool SetMoveEngagePartyPrefix(MobileParty __instance, MobileParty party, NavigationType navigationType)
    {
        if (!IsOtherPlayersRaftOrFerry(party, __instance)) return true;

        __instance.SetMoveGoToPoint(party.Position, navigationType);
        return false;
    }

    private static bool IsOtherPlayersRaftOrFerry(MobileParty target, MobileParty interactor) =>
        target != null && target != interactor && target.IsInNavalAutoTravel && target.IsPlayerParty();
}
