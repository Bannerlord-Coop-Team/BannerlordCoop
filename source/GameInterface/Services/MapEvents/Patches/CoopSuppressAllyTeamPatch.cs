using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// In a coop field battle, suppress the native player-ALLY team so all players on a side share one team.
/// <para>
/// The native <c>MissionCombatantsLogic</c> creates a separate ally team for every other combatant on the
/// player's side — so a second human player's party becomes an ally team. Under our per-client spawn model
/// each client only spawns the troops it owns, so that ally team is EMPTY on this client. The engine's spawn
/// gate (<c>DefaultBattleMissionAgentSpawnLogic.CheckDeployment</c>) refuses to spawn a side until EVERY team
/// on it has a deployment plan made, and an empty team never gets one — which blocked the entire player side
/// from ever spawning (the player ended up a spectator). Collapsing same-side players onto one team removes
/// the empty team; their troops still arrive (own spawned locally, others as puppets via
/// <c>SpawnPuppet</c>→<c>ResolveTeam(side)</c>, which already maps to the single side team).
/// </para>
/// Only active in a coop battle; ordinary battles keep their ally teams.
/// </summary>
[HarmonyPatch(typeof(MissionCombatantsLogic), "AddPlayerAllyTeam",
    new[] { typeof(BattleSideEnum), typeof(IBattleCombatant) })]
internal class CoopSuppressAllyTeamPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopSuppressAllyTeamPatch>();

    [HarmonyPrefix]
    private static bool Prefix()
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive)
            return true; // ordinary battle — keep the native ally team

        Logger.Information("[BattleSync] Suppressing player-ally team for coop battle (same-side players share one team)");
        return false; // skip — no ally team is created
    }
}
