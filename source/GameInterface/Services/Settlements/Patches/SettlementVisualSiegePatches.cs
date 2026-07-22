using Common;
using Common.Logging;
using GameInterface.Services.SiegeEngines;
using HarmonyLib;
using SandBox.View.Map.Visuals;
using Serilog;
using System.Collections.Concurrent;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// [Client] Keeps settlement map visuals from reading a partially replicated siege graph. It also repairs
/// missing bombardment state and dirties stale caches so the next sequential refresh rebuilds them.
/// </summary>
[HarmonyPatch]
internal class SettlementVisualSiegePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<SettlementVisualSiegePatches>();

    // Tick runs in a TWParallel.For worker, so log terminal states once per settlement, not per frame.
    private static readonly ConcurrentDictionary<string, byte> loggedSkips = new ConcurrentDictionary<string, byte>();

    // RefreshPartyIcon clears the dirty flag before dereferencing both siege sides, so skip the whole refresh
    // while the separately replicated camp and containers are incomplete. Vanilla then retries next frame.
    [HarmonyPatch(typeof(SettlementVisual), nameof(SettlementVisual.RefreshPartyIcon))]
    [HarmonyPrefix]
    private static bool RefreshPartyIconPrefix(SettlementVisual __instance)
    {
        if (ModInformation.IsServer) return true;

        var siegeEvent = __instance.MapEntity?.Settlement?.SiegeEvent;
        if (siegeEvent == null) return true;

        return SiegeContainerLookup.IsGraphComplete(siegeEvent);
    }

    [HarmonyPatch(typeof(SettlementVisual), nameof(SettlementVisual.RefreshSiegePreparations))]
    [HarmonyPrefix]
    private static bool RefreshSiegePreparationsPrefix(PartyBase party)
    {
        if (ModInformation.IsServer) return true;

        var siegeEvent = party?.Settlement?.SiegeEvent;
        return siegeEvent == null || SiegeContainerLookup.IsGraphComplete(siegeEvent);
    }

    [HarmonyPatch(typeof(SettlementVisual), nameof(SettlementVisual.OnMapHoverSiegeEngine))]
    [HarmonyPrefix]
    private static bool OnMapHoverSiegeEnginePrefix()
    {
        if (ModInformation.IsServer) return true;

        var siegeEvent = PlayerSiege.PlayerSiegeEvent;
        return siegeEvent == null || SiegeContainerLookup.IsGraphComplete(siegeEvent);
    }

    [HarmonyPatch(typeof(SettlementVisual), nameof(SettlementVisual.Tick))]
    [HarmonyPrefix]
    private static bool TickPrefix(SettlementVisual __instance)
    {
        if (ModInformation.IsServer) return true;

        var settlementParty = __instance.MapEntity;
        var settlement = settlementParty?.Settlement;
        if (settlement == null) return true;

        if (__instance._siegeRangedMachineEntities.Count == 0 &&
            __instance._siegeMeleeMachineEntities.Count == 0 &&
            __instance._siegeMissileEntities.Count == 0) return true;

        var siegeEvent = settlement.SiegeEvent;
        if (siegeEvent == null)
        {
            // Vanilla clears the cached entities in the sequential dirty pass right after this parallel tick.
            settlementParty.SetVisualAsDirty();
            return true;
        }

        foreach (var machineEntity in __instance._siegeRangedMachineEntities)
        {
            var side = siegeEvent.GetSiegeEventSide(machineEntity.Item2);
            var deployed = side?.SiegeEngines?.DeployedRangedSiegeEngines;
            if (deployed == null || machineEntity.Item3 >= deployed.Length)
                return SkipAndLogOnce(settlement, "a machine entity has no readable deployed-engine slot");

            var engine = deployed[machineEntity.Item3];
            if (engine == null)
            {
                // The engine left its slot without a refresh; rebuild the caches from current state.
                settlementParty.SetVisualAsDirty();
                return true;
            }

            if (engine.RangedSiegeEngine == null)
            {
                if (engine.SiegeEngine == null)
                    return SkipAndLogOnce(settlement, "a deployed engine has no engine type");

                // Reference assignment is atomic, so filling from the parallel worker is safe.
                engine.SetRangedSiegeEngine(new RangedSiegeEngine(engine.SiegeEngine, side));
                Logger.Warning("Filled missing bombardment state for {EngineType} at {Settlement}", engine.SiegeEngine.StringId, settlement.StringId);
            }
        }

        foreach (var missileEntity in __instance._siegeMissileEntities)
        {
            var side = siegeEvent.GetSiegeEventSide(missileEntity.Item2);
            if (side?.SiegeEngineMissiles == null || side.SiegeEngines?.DeployedRangedSiegeEngines == null)
                return SkipAndLogOnce(settlement, "a missile entity has no readable siege side");
        }

        return true;
    }

    private static bool SkipAndLogOnce(Settlement settlement, string reason)
    {
        if (loggedSkips.TryAdd(settlement.StringId + reason, 0))
        {
            Logger.Error("Skipping the settlement visual tick at {Settlement}: {Reason}", settlement.StringId, reason);
        }

        return false;
    }
}
