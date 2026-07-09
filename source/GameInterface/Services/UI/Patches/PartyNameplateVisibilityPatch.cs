using Common;
using HarmonyLib;
using SandBox.ViewModelCollection.Nameplate;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.Patches;

/// <summary>
/// Keeps party nameplate details within the local player's viewing distance without changing the
/// party's underlying map visibility.
/// </summary>
[HarmonyPatch]
internal class PartyNameplateVisibilityPatch
{
    private static bool hasViewingDistance;
    private static Vec2 mainPartyPosition;
    private static float viewingDistanceSquared;

    [HarmonyPatch(typeof(PartyNameplatesVM), nameof(PartyNameplatesVM.Update))]
    [HarmonyPrefix]
    private static void CacheViewingDistance()
    {
        hasViewingDistance = false;
        if (ModInformation.IsServer) return;

        var mainParty = MobileParty.MainParty;
        if (mainParty == null) return;

        float viewingDistance = mainParty.SeeingRange;
        if (viewingDistance <= 0f) return;

        mainPartyPosition = mainParty.Position.ToVec2();
        viewingDistanceSquared = viewingDistance * viewingDistance;
        hasViewingDistance = true;
    }

    [HarmonyPatch(typeof(PartyNameplateVM), nameof(PartyNameplateVM.RefreshBinding))]
    [HarmonyPrefix]
    private static void ClampVisibility(PartyNameplateVM __instance)
    {
        if (!hasViewingDistance || !__instance._isVisibleOnMapBind) return;

        var party = __instance.Party;
        if (party == null || party.IsMainParty) return;

        if (party.Position.ToVec2().DistanceSquared(mainPartyPosition) > viewingDistanceSquared)
        {
            __instance._isVisibleOnMapBind = false;
        }
    }
}
