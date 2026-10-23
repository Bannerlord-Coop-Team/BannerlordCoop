using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.SiegeEngines.Messages;
using HarmonyLib;
using Serilog;
using SandBox.ViewModelCollection.MapSiege;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEngines.Patches;

/// <summary>
/// Routes the client's siege production click to the server, because the vanilla click builds an unregistered ghost engine.
/// </summary>
[HarmonyPatch(typeof(MapSiegeProductionVM))]
internal class MapSiegeProductionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapSiegeProductionPatches>();

    // The server's DeploySiegeEngine mirrors the construct-or-reuse-then-deploy and the move-to-reserve branch reuses
    // the removal request; only deploy builds the ghost, but routing both keeps the click on one server-authoritative path.
    [HarmonyPatch(nameof(MapSiegeProductionVM.OnPossibleMachineSelection))]
    [HarmonyPrefix]
    private static bool OnPossibleMachineSelectionPrefix(MapSiegeProductionVM __instance, MapSiegeProductionMachineVM machine)
    {
        if (ModInformation.IsServer) return true;

        var siege = PlayerSiege.PlayerSiegeEvent;
        var poi = __instance.LatestSelectedPOI;
        if (siege == null || poi == null)
        {
            Logger.Warning("Client siege production click missing state; allowing vanilla. SiegeEventNull={SiegeEventNull}, SelectedPOINull={SelectedPOINull}",
                siege == null, poi == null);
            return true;
        }

        // Vanilla ends every machine click with IsEnabled = false, dismissing the selection popup; the
        // skipped original never runs, so close it here on each routed path.
        if (poi.Machine != null && poi.Machine.SiegeEngine == machine.Engine)
        {
            // Vanilla no-ops when the slot already holds the clicked engine.
            __instance.IsEnabled = false;
            return false;
        }

        var side = PlayerSiege.PlayerSide;
        var container = siege.GetSiegeEventSide(side)?.SiegeEngines;
        if (container == null)
        {
            Logger.Warning("Client siege production click could not resolve the {Side} engine container", side);
            __instance.IsEnabled = false;
            return false;
        }

        if (machine.IsReserveOption && poi.Machine != null)
        {
            bool moveToReserve = poi.Machine.IsActive || poi.Machine.IsBeingRedeployed;
            MessageBroker.Instance.Publish(machine, new SiegeEngineRemovalRequested(
                siege,
                container,
                side,
                poi.MachineIndex,
                poi.Machine.SiegeEngine.IsRanged,
                moveToReserve,
                poi.Machine));
        }
        else
        {
            MessageBroker.Instance.Publish(machine, new SiegeEngineDeployRequested(
                siege,
                container,
                side,
                machine.Engine,
                poi.MachineIndex,
                poi.Machine));
        }

        __instance.IsEnabled = false;
        return false;
    }
}
