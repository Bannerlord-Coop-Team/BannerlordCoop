using Common.Logging;
using Common.Util;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Patches;

/// <summary>
/// Clears saved blood feuds before load-time listeners read them, so no peer shows the save's feuds as its own.
/// </summary>
[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnGameLoaded))]
internal class BloodFeudRepairPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<BloodFeudRepairPatch>();

    /// <summary>
    /// Runs the feud repair on every clan in the loaded campaign.
    /// </summary>
    [HarmonyPrefix]
    private static void ClearPlayerFeuds()
    {
        if (!ContainerProvider.TryResolve<IBloodFeudRepairer>(out var repairer))
        {
            Logger.Error("Unable to resolve {Repairer}", nameof(IBloodFeudRepairer));
            return;
        }

        repairer.ClearPlayerFeuds(Clan.All);
    }
}
