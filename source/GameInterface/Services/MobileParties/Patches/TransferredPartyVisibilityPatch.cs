using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Hides every party in the save a client loads from the server, before the map visuals start.
/// </summary>
[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnGameLoaded))]
internal class TransferredPartyVisibilityPatch
{
    [HarmonyPrefix]
    private static void Prefix() => ClearServerVisibility(Campaign.Current);

    // The server keeps every active party visible (PartyVisibilityOnServerPatch), so the save it sends marks the whole
    // map visible. Map visuals take their first alpha from that and visible ones build their ship models, which crashes
    // a client with hundreds of fleets. OnGameLoaded runs after the save system's late initialize re-reads each party's
    // saved fields and before the naval visuals start; PartyVisibilitySweep then reveals the parties in range. Client
    // patches only apply after joining, so every save a client loads here came from the server. Debug builds keep
    // everything visible.
    internal static void ClearServerVisibility(Campaign campaign)
    {
        if (ModInformation.IsServer || DebugPartyVisibility.ForceAllVisible) return;

        foreach (var party in campaign.MobileParties)
        {
            party._isVisible = false;
        }
    }
}
