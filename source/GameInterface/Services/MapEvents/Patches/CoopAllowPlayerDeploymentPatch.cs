using HarmonyLib;
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
        if (BattleSpawnGate.IsCoopBattleActive)
            __result = true;
    }
}
