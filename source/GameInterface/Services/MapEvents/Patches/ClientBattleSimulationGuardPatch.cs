using Common;
using Common.Logging;
using GameInterface.Policies;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Stops a client locally simulating a battle the server owns.
/// </summary>
/// <remarks>
/// Leaving an encounter runs <c>MenuHelper.EncounterLeaveConsequence</c>, whose tail simulates whatever is
/// left of the battle when the player walks away from one still in progress:
///
///     battle.SimulateBattleSetup(PlayerEncounter.Current?.BattleSimulation?.SelectedTroops);
///     battle.SimulateBattleRound(...);
///
/// In coop that decision belongs to the server, which has already resolved the event - so this is a desync
/// by construction. It is also an outright crash. <c>SimulateBattleSetup</c> indexes the roster array by each
/// side's mission side:
///
///     roster = selectedTroops == null ? null : selectedTroops[side.MissionSide];
///
/// and on a client whose sides were never assigned one, <c>MissionSide</c> is <c>None</c> - which is -1, not a
/// valid index. Observed live after a won sally-out: every click of "Leave..." threw
/// IndexOutOfRangeException out of the menu consequence, so the encounter never finished and the player was
/// pinned on the menu with no way off it while everyone else had moved on.
///
/// Scoped tightly: only on a client, only outside an authoritative replay, and only for a map event the
/// object manager knows - a purely local event (one this client legitimately owns end to end) still
/// simulates normally. The server's own simulation path is untouched, and it passes null for the roster,
/// which is the branch that never indexes.
/// </remarks>
[HarmonyPatch]
internal class ClientBattleSimulationGuardPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClientBattleSimulationGuardPatch>();

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.SimulateBattleSetup), new[] { typeof(FlattenedTroopRoster[]) })]
    [HarmonyPrefix]
    private static bool SimulateBattleSetupPrefix(MapEvent __instance)
        => !SuppressLocalSimulation(__instance, nameof(MapEvent.SimulateBattleSetup));

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.SimulateBattleRound), new[] { typeof(int), typeof(int) })]
    [HarmonyPrefix]
    private static bool SimulateBattleRoundPrefix(MapEvent __instance)
        => !SuppressLocalSimulation(__instance, nameof(MapEvent.SimulateBattleRound));

    private static bool SuppressLocalSimulation(MapEvent mapEvent, string step)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return false;
        if (ModInformation.IsServer) return false;
        if (mapEvent == null) return false;

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return false;
        if (!objectManager.TryGetId(mapEvent, out var mapEventId)) return false;

        Logger.Warning(
            "[BattleSync] Skipping local {Step} for server-owned battle {MapEventId}; the server resolves it",
            step, mapEventId);
        return true;
    }
}
