using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.SiegeEngines.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines.Patches;

/// <summary>
/// Server-side sync of the <see cref="SiegeEnginesContainer"/> mutation methods. Clients re-run the
/// same method under AllowedThread when the server's change replicates, keeping the deployed arrays,
/// reserve list and count dictionaries consistent without syncing them element by element.
/// </summary>
[HarmonyPatch(typeof(SiegeEnginesContainer))]
internal class SiegeEnginesContainerPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeEnginesContainerPatches>();

    [HarmonyPatch(nameof(SiegeEnginesContainer.DeploySiegeEngineAtIndex))]
    [HarmonyPrefix]
    private static bool DeploySiegeEngineAtIndexPrefix(SiegeEnginesContainer __instance, SiegeEngineConstructionProgress siegeEngine, int index)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            // The map production popup is the only client caller; route the build order to the server.
            // The progress object the popup made locally is an unregistered ghost, so the request
            // carries the engine type and the server does its own reserve-lookup-or-create.
            if (!TryDescribeContainer(__instance, out var siegeEvent, out var side))
            {
                Logger.Error("Client tried to deploy a siege engine on a container outside its own siege");
                return false;
            }

            MessageBroker.Instance.Publish(__instance, new SiegeEngineDeployRequested(siegeEvent, side, siegeEngine.SiegeEngine, index));
            return false;
        }

        MessageBroker.Instance.Publish(__instance, new SiegeEngineDeployed(__instance, siegeEngine, index));
        return true;
    }

    [HarmonyPatch(nameof(SiegeEnginesContainer.RemoveDeployedSiegeEngine))]
    [HarmonyPrefix]
    private static bool RemoveDeployedSiegeEnginePrefix(SiegeEnginesContainer __instance, int index, bool isRanged, bool moveToReserve)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            if (!TryDescribeContainer(__instance, out var siegeEvent, out var side))
            {
                Logger.Error("Client tried to remove a siege engine on a container outside its own siege");
                return false;
            }

            MessageBroker.Instance.Publish(__instance, new SiegeEngineRemovalRequested(siegeEvent, side, index, isRanged, moveToReserve));
            return false;
        }

        MessageBroker.Instance.Publish(__instance, new SiegeEngineUndeployed(__instance, index, isRanged, moveToReserve));
        return true;
    }

    // The container has no back-reference to its siege or side; on a client the only native caller is
    // the local player's production popup, so the player's own siege identifies it.
    private static bool TryDescribeContainer(SiegeEnginesContainer container, out SiegeEvent siegeEvent, out BattleSideEnum side)
    {
        siegeEvent = PlayerSiege.PlayerSiegeEvent;
        side = BattleSideEnum.None;
        if (siegeEvent == null) return false;

        if (siegeEvent.BesiegerCamp?.SiegeEngines == container)
        {
            side = BattleSideEnum.Attacker;
            return true;
        }

        if (siegeEvent.BesiegedSettlement?.SiegeEngines == container)
        {
            side = BattleSideEnum.Defender;
            return true;
        }

        return false;
    }

    [HarmonyPatch(nameof(SiegeEnginesContainer.AddPrebuiltEngineToReserve))]
    [HarmonyPrefix]
    private static bool AddPrebuiltEngineToReservePrefix(SiegeEnginesContainer __instance, SiegeEngineConstructionProgress siegeEngine)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to add a prebuilt siege engine outside a synced flow");
            return false;
        }

        MessageBroker.Instance.Publish(__instance, new SiegeEngineReserveAdded(__instance, siegeEngine));
        return true;
    }

    [HarmonyPatch(nameof(SiegeEnginesContainer.RemovedSiegeEngineFromReservedSiegeEngines))]
    [HarmonyPrefix]
    private static bool RemovedFromReservePrefix(SiegeEnginesContainer __instance, SiegeEngineConstructionProgress siegeEngine)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to remove a reserved siege engine outside a synced flow");
            return false;
        }

        MessageBroker.Instance.Publish(__instance, new SiegeEngineReserveRemoved(__instance, siegeEngine));
        return true;
    }

    internal static void RunDeploySiegeEngineAtIndex(SiegeEnginesContainer container, SiegeEngineConstructionProgress siegeEngine, int index)
    {
        using (new AllowedThread())
        {
            container.DeploySiegeEngineAtIndex(siegeEngine, index);
        }
    }

    internal static void RunRemoveDeployedSiegeEngine(SiegeEnginesContainer container, int index, bool isRanged, bool moveToReserve)
    {
        using (new AllowedThread())
        {
            container.RemoveDeployedSiegeEngine(index, isRanged, moveToReserve);
        }
    }

    internal static void RunAddPrebuiltEngineToReserve(SiegeEnginesContainer container, SiegeEngineConstructionProgress siegeEngine)
    {
        using (new AllowedThread())
        {
            container.AddPrebuiltEngineToReserve(siegeEngine);
        }
    }

    internal static void RunRemovedSiegeEngineFromReserve(SiegeEnginesContainer container, SiegeEngineConstructionProgress siegeEngine)
    {
        using (new AllowedThread())
        {
            container.RemovedSiegeEngineFromReservedSiegeEngines(siegeEngine);
        }
    }
}
