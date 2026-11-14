using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Hides every party in the save a client loads from the server, before the map visuals start.
/// </summary>
[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnGameLoaded))]
internal class TransferredPartyVisibilityPatch
{
    // The server's save marks every party visible, and visible fleets build ship models on the first map frames.
    // OnGameLoaded runs after the late initialize re-reads saved fields, so the clear holds.
    [HarmonyPrefix]
    private static void HideTransferredParties()
    {
        if (!ContainerProvider.TryResolve<IPartyVisibilitySweep>(out var sweep)) return;

        sweep.HideAll(Campaign.Current.MobileParties);
    }
}
