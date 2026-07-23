using Common.Messaging;
using GameInterface;
using GameInterface.Services.Locations;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using Missions.Agents.Extensions;
using Missions.Missiles.Message;
using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Missiles.Patches
{
    /// <summary>
    /// MissileAuxPatch to send shoot event and returned resulting integer
    /// </summary>
    [HarmonyPatch(typeof(Mission), "AddMissileAux")]
    [HarmonyPatchCategory(MissionModule.MissilePatchCategory)]
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
            BlockMissileIfNative.PublishAgentShot(__result, shooterAgent, direction, position,
                orientation, baseSpeed, speed, addRigidBody, __state);
        }
    }
    /// <summary>
    /// MissileSingleUsageAuxPatch to send shoot event and returned resulting integer
    /// </summary>
    [HarmonyPatch(typeof(Mission), "AddMissileSingleUsageAux")]
    [HarmonyPatchCategory(MissionModule.MissilePatchCategory)]
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
            BlockMissileIfNative.PublishAgentShot(__result, shooterAgent, direction, position,
                orientation, baseSpeed, speed, addRigidBody, __state);
        }
    }
    /// <summary>
    /// Block the locally created missile
    /// </summary>
    [HarmonyPatch(typeof(Mission), "OnAgentShootMissile")]
    [HarmonyPatchCategory(MissionModule.MissilePatchCategory)]
    public static class BlockMissileIfNative
    {
        [ThreadStatic]
        private static bool capturingAgentShot;

        internal static bool IsCapturingAgentShot => capturingAgentShot;
        internal static bool IsCoopMissionActive =>
            BattleSpawnGate.IsCoopBattleActive || LocationMissionTracker.IsLocationMission(Mission.Current);

        internal static void PublishAgentShot(int missileIndex, Agent shooter, Vec3 direction, Vec3 position,
            Mat3 orientation, float baseSpeed, float speed, bool hasRigidBody, MissionWeapon weapon)
        {
            if (!capturingAgentShot || !IsCoopMissionActive || !shooter.IsLocallyControlled())
                return;

            MessageBroker.Instance.Publish(shooter, new AgentShoot(shooter, weapon, position, direction,
                orientation, baseSpeed, speed, hasRigidBody, missileIndex));
        }

        [HarmonyPrefix]
        public static bool OnAgentShootMissile(
            ref Agent shooterAgent,
            ref int forcedMissileIndex)
        {
            if (!IsCoopMissionActive)
                return true;

            if (forcedMissileIndex == -1 &&
                ContainerProvider.TryResolve<INetworkAgentRegistry>(out var agentRegistry) &&
                agentRegistry.TryGetAgentInfo(shooterAgent, out _) &&
                !agentRegistry.IsLocallyControlled(shooterAgent))
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
