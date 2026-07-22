using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// BR-110: the native reinforcement reserve (<c>_reservedTroops</c>, filled once per wave from
/// <c>CoopTroopSupplier.SupplyTroops</c>) drains over many ticks via
/// <see cref="MissionBattleSideSpawnContext.SpawnTroops(int, bool)"/>. The supply-time clamp is only a
/// snapshot — while a wave drains, other spawn paths (puppets, mid-battle reinforcements) consume slots, so
/// the reserved wave could still push the mission past the engine agent limit. This prefix re-checks the LIVE
/// remaining capacity every time the native drip spawns, deferring the rest of the reserve until removals free
/// slots. Only active inside a coop battle (see <see cref="BattleSpawnGate"/>); ordinary battles are untouched.
/// <para>
/// Clamping is at troop granularity — the engine mints a cavalry rider's mount internally, so a drip that
/// crosses the limit with cavalry can momentarily overshoot by that batch's mount count; the next tick sees
/// the higher live count and clamps to zero, so any overshoot is a bounded, self-correcting transient.
/// </para>
/// </summary>
[HarmonyPatch(typeof(MissionBattleSideSpawnContext), nameof(MissionBattleSideSpawnContext.SpawnTroops),
    new[] { typeof(int), typeof(bool) })]
internal class MissionSpawnCapacityPatch
{
    [HarmonyPrefix]
    private static void Prefix(ref int number)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return;
        if (!ContainerProvider.TryResolve<IBattleAgentBudget>(out var budget)) return;

        number = ClampSpawnNumber(Mission.Current, budget, number);
    }

    /// <summary>Clamp a native drip of <paramref name="number"/> troops to the mission's live remaining
    /// capacity; a null mission or budget, or a non-positive request, is left unchanged.</summary>
    internal static int ClampSpawnNumber(Mission mission, IBattleAgentBudget budget, int number)
    {
        if (mission == null || budget == null || number <= 0) return number;
        return budget.ClampToCapacity(mission, number);
    }
}
