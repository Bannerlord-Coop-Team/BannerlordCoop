using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// In a coop field battle every player deploys their OWN troops — each client fields only its own party, so the
/// formations it commands during deployment contain just that player's troops. The native gate
/// (<c>SandboxBattleInitializationModel.CanPlayerSideDeployWithOrderOfBattleAux</c>) only lets the side's LEADER
/// party deploy with the Order of Battle (and only at 20+ troops), so for any non-leader, non-sergeant party —
/// e.g. a second player's separate individual party — it returns false and the deployment auto-finishes,
/// skipping that player's deployment stage entirely. Force it on for coop battles so every client gets its own
/// Order-of-Battle phase for its own troops.
/// <para>
/// Exception — NO LEADER ON THE FIELD: if the local player's own hero has no live agent in the mission, there is
/// no <c>Agent.Main</c> to command the Order of Battle. Opening the OoB leaderless softlocks it (the player can
/// neither place a formation nor click Start Battle). The prime case is a hero DOWNED in a live coop battle who
/// then LEAVES and REJOINS: the downed hero is no longer supplied into the fight, so it never respawns, and the
/// rejoin (a mid-battle join) is still forced through the OoB — which then hangs. When the hero is absent we force
/// the gate OFF instead, so native <c>SetupTeams</c> runs <c>FinishDeployment</c> immediately: the player drops
/// straight into the live battle (spectating / commanding via <c>SetPlayerCanTakeControlOfAnotherAgentWhenDead</c>)
/// rather than a leaderless deploy screen. This also covers a wounded hero who explicitly starts a fresh battle
/// (the native flatten excludes it, so it likewise never spawns).
/// </para>
/// <para>
/// Timing note: this gate is evaluated at the END of <c>DeploymentMissionController.SetupTeams</c>, i.e. AFTER the
/// player side has spawned (<c>SetSpawnTroops(enforceSpawning)</c> → <c>CheckDeployment</c> runs synchronously).
/// So a healthy hero already has its agent on the field by the time we read it here — only a genuinely absent hero
/// trips the override. (The only other caller, the OoB VM's <c>SaveConfiguration</c>, runs later still.)
/// </para>
/// </summary>
// Single parameterless method — no overload ambiguity, so the direct attribute target is safe.
[HarmonyPatch(typeof(BattleInitializationModel), nameof(BattleInitializationModel.CanPlayerSideDeployWithOrderOfBattle))]
internal class CoopAllowPlayerDeploymentPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref bool __result)
    {
        // BattleSpawnGate is active for the lifetime of a coop battle mission (engaged in OpenAttackMission on
        // every client). Only override inside one, so native single-player deployment is untouched.
        if (!BattleSpawnGate.IsCoopBattleActive)
            return;

        // No local leader on the field → skip the OoB (auto-FinishDeployment) instead of softlocking on a
        // leaderless deploy screen. Otherwise force it on so every coop client deploys its own party.
        __result = LocalPlayerHeroOnField();
    }

    // True if the local player's own hero has a live agent in the current mission. False for a hero downed in a
    // live battle that then rejoins (no longer supplied → never spawns) or a wounded hero explicitly starting a
    // fresh battle (excluded from the flatten) — in both cases there is no Agent.Main to command the Order of Battle.
    private static bool LocalPlayerHeroOnField()
    {
        var mission = Mission.Current;
        var heroCharacter = Hero.MainHero?.CharacterObject;
        if (mission == null || heroCharacter == null)
            return false;

        foreach (var agent in mission.Agents)
            if (agent.IsActive() && agent.Character == heroCharacter)
                return true;

        return false;
    }
}
