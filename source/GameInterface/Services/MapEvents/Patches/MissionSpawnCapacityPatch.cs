using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// BR-110: the native reinforcement reserve (<c>_reservedTroops</c>, filled once per wave from
/// <c>CoopTroopSupplier.SupplyTroops</c>) drains over many ticks via
/// <see cref="MissionBattleSideSpawnContext.SpawnTroops(int, bool)"/>. The supply-time clamp is only a
/// snapshot — while a wave drains, other spawn paths (puppets, mid-battle reinforcements) consume slots, so
/// the reserved wave could still push the mission past the engine agent limit. This prefix re-clamps every
/// native drip to the LIVE remaining capacity in RENDER SLOTS: it walks the reserved origins the drip would
/// spawn front-to-back and charges each troop its own slot cost (a mounted troop spawns rider + horse in one
/// call — two slots), stopping at the first troop that no longer fits. A cavalry drip with a single slot left
/// therefore spawns nothing — never 2001. Deferred troops stay reserved and the drip retries as removals free
/// slots. Only active inside a coop battle (see <see cref="BattleSpawnGate"/>); ordinary battles are untouched.
/// </summary>
[HarmonyPatch(typeof(MissionBattleSideSpawnContext), nameof(MissionBattleSideSpawnContext.SpawnTroops),
    new[] { typeof(int), typeof(bool) })]
internal class MissionSpawnCapacityPatch
{
    private static readonly AccessTools.FieldRef<MissionBattleSideSpawnContext, List<IAgentOriginBase>> ReservedTroops =
        AccessTools.FieldRefAccess<MissionBattleSideSpawnContext, List<IAgentOriginBase>>("_reservedTroops");

    [HarmonyPrefix]
    private static void Prefix(MissionBattleSideSpawnContext __instance, ref int number)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return;
        if (!ContainerProvider.TryResolve<IBattleAgentBudget>(out var budget)) return;

        number = ClampSpawnNumber(Mission.Current, budget, number,
            ReservedTroops(__instance), __instance.SpawnWithHorses, __instance.ForceSpawnPlayerMounted);
    }

    /// <summary>Clamp a native drip of <paramref name="number"/> troops to the mission's live remaining
    /// capacity in render slots. The drip spawns <paramref name="reservedTroops"/> front-to-back, so each
    /// troop is charged its own slot cost in that order and the walk stops at the first troop that does not
    /// fit — never skipping ahead of the FIFO reserve. A troop past the end of the reserve would be topped up
    /// from the supplier — unknown here, so it is charged the mounted worst case of two slots (both native
    /// call sites request within the reserve — the initial spawn is gated on a full reserve, the reinforcement
    /// drip on a non-empty one — so the conservative charge can only ever under-spawn, and the supplier's own
    /// slot-aware clamp still refines it). A null mission or budget, or a non-positive request, is left
    /// unchanged.</summary>
    internal static int ClampSpawnNumber(Mission mission, IBattleAgentBudget budget, int number,
        IReadOnlyList<IAgentOriginBase> reservedTroops, bool spawnWithHorses, bool forceSpawnPlayerMounted)
    {
        if (mission == null || budget == null || number <= 0) return number;

        int remaining = budget.RemainingCapacity(budget.CountLiveAgents(mission));
        int reservedCount = reservedTroops?.Count ?? 0;
        int allowed = 0;
        while (allowed < number)
        {
            int slots = allowed < reservedCount
                ? SlotsForReservedTroop(budget, reservedTroops[allowed], spawnWithHorses, forceSpawnPlayerMounted)
                : 2;
            if (slots > remaining) break;
            remaining -= slots;
            allowed++;
        }
        return allowed;
    }

    // Render slots one reserved origin consumes when the native drip spawns it. Mirrors the drip's own spawn
    // condition: a horse is minted only when the side spawns with horses (or the player is force-mounted) AND
    // the troop's equipment actually mounts one; a dismounted spawn still renders the rider.
    private static int SlotsForReservedTroop(IBattleAgentBudget budget, IAgentOriginBase origin,
        bool spawnWithHorses, bool forceSpawnPlayerMounted)
    {
        if (origin == null) return 0;

        bool withHorse = spawnWithHorses || (forceSpawnPlayerMounted && origin.Troop?.IsPlayerCharacter == true);
        return withHorse ? budget.SlotsForOrigin(origin) : 1;
    }
}
