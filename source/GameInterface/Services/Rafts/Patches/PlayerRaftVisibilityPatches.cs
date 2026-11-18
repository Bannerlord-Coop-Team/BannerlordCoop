using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using SandBox.ViewModelCollection.Nameplate;
using TaleWorlds.CampaignSystem.Party;
using static TaleWorlds.CampaignSystem.Party.MobileParty;

namespace GameInterface.Services.Rafts.Patches;

/// <summary>
/// Other players' rafts stay visible with their nameplate but can't be engaged, so a player sailing back
/// after escaping captivity can be seen without being attacked or talked to.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class PlayerRaftVisibilityPatches
{
    // Remembers whether the camera left the nameplate visible before vanilla's hide rules run
    [HarmonyPatch(typeof(PartyNameplateVM), nameof(PartyNameplateVM.RefreshDynamicProperties))]
    [HarmonyPrefix]
    private static void RefreshDynamicPropertiesPrefix(PartyNameplateVM __instance, out bool __state) => __state = __instance._isVisibleOnMapBind;

    // Vanilla hides the nameplate of every non-main party in naval auto travel (a raft or a ferry), so show other players' rafts again
    [HarmonyPatch(typeof(PartyNameplateVM), nameof(PartyNameplateVM.RefreshDynamicProperties))]
    [HarmonyPostfix]
    private static void RefreshDynamicPropertiesPostfix(PartyNameplateVM __instance, bool __state)
    {
        var party = __instance.Party;
        var showAgain = ShouldShowAgain(
            wasVisible: __state,
            isVisible: __instance._isVisibleOnMapBind,
            isInArmy: __instance._isInArmyBind,
            isInSettlement: __instance._isInSettlementBind,
            isInNavalAutoTravel: party.IsInNavalAutoTravel);

        if (!showAgain) return;
        if (!IsOtherPlayersRaft(party, MobileParty.MainParty)) return;

        __instance._isVisibleOnMapBind = true;
    }

    // True when this refresh hid a visible nameplate only because the party is in naval auto travel (a raft or a ferry); vanilla's army and settlement hides still apply
    internal static bool ShouldShowAgain(bool wasVisible, bool isVisible, bool isInArmy, bool isInSettlement, bool isInNavalAutoTravel) =>
        wasVisible && !isVisible && !isInArmy && !isInSettlement && isInNavalAutoTravel;

    [HarmonyPatch(typeof(PartyBase), "TaleWorlds.CampaignSystem.Map.IInteractablePoint.CanPartyInteract")]
    [HarmonyPostfix]
    private static void CanPartyInteractPostfix(PartyBase __instance, MobileParty mobileParty, ref bool __result)
    {
        if (!__result) return;

        if (IsOtherPlayersRaft(__instance.MobileParty, mobileParty))
            __result = false;
    }

    // A click on another player's raft moves next to it instead of following it forever
    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.SetMoveEngageParty))]
    [HarmonyPrefix]
    private static bool SetMoveEngagePartyPrefix(MobileParty __instance, MobileParty party, NavigationType navigationType)
    {
        if (!IsOtherPlayersRaft(party, __instance)) return true;

        __instance.SetMoveGoToPoint(party.Position, navigationType);
        return false;
    }

    private static bool IsOtherPlayersRaft(MobileParty target, MobileParty interactor) =>
        target != null && target != interactor && target.IsInRaftState && target.IsPlayerParty();
}
