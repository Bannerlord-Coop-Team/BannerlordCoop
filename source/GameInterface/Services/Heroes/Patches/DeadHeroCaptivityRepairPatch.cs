using Common.Logging;
using Common.Util;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnGameLoaded))]
internal class DeadHeroCaptivityRepairPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<DeadHeroCaptivityRepairPatch>();

    [HarmonyPrefix]
    private static void RepairCaptivityRosters()
    {
        if (!ContainerProvider.TryResolve<IDeadHeroCaptivityRepairer>(out var repairer))
        {
            Logger.Error("Unable to resolve {Repairer}", nameof(IDeadHeroCaptivityRepairer));
            return;
        }

        repairer.RepairLoadedState(Campaign.Current.CampaignObjectManager);
    }
}
