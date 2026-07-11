using Common;
using Common.Logging;
using HarmonyLib;
using SandBox.View.Map.Visuals;
using Serilog;
using System.Collections.Concurrent;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// [Client] Validates the siege data the settlement visual is about to deref. Tick has no null guards and
/// its NRE escapes to Game.OnTick, so one hole in the replicated siege graph freezes campaign time and
/// menus every frame. Heals a deployed ranged engine missing its bombardment state, dirties the visual
/// when a cached entity's backing slot is gone (the sequential refresh rebuilds the caches), skips
/// the tick when the graph is unreadable, and skips the icon rebuild while the siege graph is still
/// replicating.
/// </summary>
[HarmonyPatch]
internal class SettlementVisualSiegePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<SettlementVisualSiegePatches>();

    // Tick runs in a TWParallel.For worker, so log terminal states once per settlement, not per frame.
    private static readonly ConcurrentDictionary<string, byte> loggedSkips = new ConcurrentDictionary<string, byte>();

    // The rebuild derefs both siege sides' engine containers; during the SiegeEvent constructor's
    // replication window the event exists before its camp and containers do. Skip the rebuild —
    // the next visual-dirty re-runs it against the completed graph.
    [HarmonyPatch(typeof(SettlementVisual), nameof(SettlementVisual.AddSiegeIconComponents))]
    [HarmonyPrefix]
    private static bool AddSiegeIconComponentsPrefix(PartyBase party)
    {
        if (ModInformation.IsServer) return true;

        var siegeEvent = party?.Settlement?.SiegeEvent;
        if (siegeEvent == null) return true;

        return siegeEvent.BesiegerCamp != null
            && siegeEvent.GetSiegeEventSide(BattleSideEnum.Attacker)?.SiegeEngines != null
            && siegeEvent.GetSiegeEventSide(BattleSideEnum.Defender)?.SiegeEngines != null;
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
