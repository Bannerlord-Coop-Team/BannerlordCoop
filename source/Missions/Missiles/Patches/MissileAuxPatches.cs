using Common.Messaging;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using Missions.Agents.Extensions;
using Missions.Missiles.Message;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Missiles.Patches
{
    /// <summary>
    /// MissileAuxPatch to send shoot event and returned resulting integer
    /// </summary>
    [HarmonyPatch(typeof(Mission), "AddMissileAux")]
    [HarmonyPatchCategory(MissilePatchInstaller.MissilePatchCategory)]
    public class AddMissileAuxPatch
    {
        private static void Prefix(Agent shooterAgent, ref MissionWeapon __state)
        {
            if (!BlockMissileIfNative.IsCapturingAgentShot)
                return;

            __state = shooterAgent.WieldedWeapon;
        }
        private static void Postfix(
            int __result,
            Agent shooterAgent,
            ref Vec3 direction,
            ref Vec3 position,
            ref Mat3 orientation,
            float baseSpeed,
            float speed,
            bool addRigidBody,
            ref MissionWeapon __state)
        {
            if (!BlockMissileIfNative.IsCapturingAgentShot)
                return;

            if (!BattleSpawnGate.IsCoopBattleActive)
                return;

            if (!shooterAgent.IsLocallyControlled())
                return;

            AgentShoot message = new AgentShoot(
                shooterAgent,
                __state,
                position,
                direction,
                orientation,
                baseSpeed,
                speed,
                addRigidBody,
                __result);
            MessageBroker.Instance.Publish(shooterAgent, message);
        }
    }
    /// <summary>
    /// MissileSingleUsageAuxPatch to send shoot event and returned resulting integer
    /// </summary>
    [HarmonyPatch(typeof(Mission), "AddMissileSingleUsageAux")]
    [HarmonyPatchCategory(MissilePatchInstaller.MissilePatchCategory)]
    public class AddMissileSingleUsageAuxPatch
    {
        private static void Prefix(Agent shooterAgent, ref MissionWeapon __state)
        {
            if (!BlockMissileIfNative.IsCapturingAgentShot)
                return;

            __state = shooterAgent.WieldedWeapon;
        }
        private static void Postfix(int __result,
            Agent shooterAgent,
            ref Vec3 direction,
            ref Vec3 position,
            ref Mat3 orientation,
            float baseSpeed,
            float speed,
            bool addRigidBody,
            ref MissionWeapon __state)
        {
            if (!BlockMissileIfNative.IsCapturingAgentShot)
                return;

            if (!BattleSpawnGate.IsCoopBattleActive)
                return;

            if (!shooterAgent.IsLocallyControlled())
                return;

            AgentShoot message = new AgentShoot(
                shooterAgent,
                __state,
                position,
                direction,
                orientation,
                baseSpeed,
                speed,
                addRigidBody,
                __result);
            MessageBroker.Instance.Publish(shooterAgent, message);
        }
    }
    /// <summary>
    /// Block the locally created missile
    /// </summary>
    [HarmonyPatch(typeof(Mission), "OnAgentShootMissile")]
    [HarmonyPatchCategory(MissilePatchInstaller.MissilePatchCategory)]
    public static class BlockMissileIfNative
    {
        [System.ThreadStatic]
        private static bool capturingAgentShot;

        internal static bool IsCapturingAgentShot => capturingAgentShot;

        [HarmonyPrefix]
        public static bool OnAgentShootMissile(
            ref Agent shooterAgent,
            ref int forcedMissileIndex)
        {
            // Outside a coop battle (tournaments, arenas, locations, vanilla missions) let the engine make its
            // own missile; we only replace native creation with the networked one for peer-owned battle agents.
            if (!BattleSpawnGate.IsCoopBattleActive)
                return true;

            if (!shooterAgent.IsLocallyControlled() && forcedMissileIndex == -1)
            {
                return false;
            }

            // AddCustomMissile reaches the same low-level helpers (notably for siege weapons). Bracket only
            // vanilla agent shots so those dedicated custom-missile replication paths remain their sole owner.
            capturingAgentShot = true;
            return true;
        }

        [HarmonyFinalizer]
        private static void Finalizer()
        {
            capturingAgentShot = false;
        }
    }
}
