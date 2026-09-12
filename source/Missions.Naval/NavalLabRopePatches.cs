#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NavalDLC.Missions.NavalPhysics;
using NavalDLC.Missions.Objects.UsableMachines;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using ForceMode = TaleWorlds.Engine.GameEntityPhysicsExtensions.ForceMode;
using Attachment = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment;
using Joint = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachmentJoint;
using RopeState = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment.ShipAttachmentState;

namespace Missions.Naval;

internal static class NavalLabRopePatches
{
    [ThreadStatic] private static NavalLabBehavior solver;

    [HarmonyPatch(typeof(ShipAttachmentMachine), nameof(ShipAttachmentMachine.ConnectWithAttachmentPointMachine))]
    private static class Connect
    {
        private static bool Prefix(ShipAttachmentMachine __instance, ShipAttachmentPointMachine attachmentPointMachine, bool forceBridge) =>
            NavalLabPhysicsPatches.Active?.AllowRopeConnection(__instance, attachmentPointMachine, forceBridge) != false;
        private static void Postfix(ShipAttachmentMachine __instance) => NavalLabPhysicsPatches.Active?.ObserveRopeCreated(__instance);
    }

    [HarmonyPatch(typeof(Attachment), nameof(Attachment.CheckAndConnectBridge))]
    private static class NoBridge
    {
        private static bool Prefix(Attachment __instance) => NavalLabPhysicsPatches.Active?.AllowRopeBridge(__instance) != false;
    }

    [HarmonyPatch(typeof(ShipAttachmentMachineConnectionLogic), "OnTick")]
    private static class FixedEndpoints
    {
        private static bool Prefix(ShipAttachmentMachineConnectionLogic __instance)
        {
            var active = NavalLabPhysicsPatches.Active;
            // This component only retargets by destroying and recreating the connection.
            return active?.HasRopeExperiment != true || Array.IndexOf(active.Ships, __instance._ownerShip) < 0;
        }
    }

    [HarmonyPatch(typeof(Attachment), nameof(Attachment.SetAttachmentState))]
    private static class State
    {
        private static bool Prefix(Attachment __instance, RopeState state) => NavalLabPhysicsPatches.Active?.AllowRopeState(__instance, state) != false;
    }

    [HarmonyPatch(typeof(Attachment), nameof(Attachment.OnTick))]
    private static class ReplicaTick
    {
        private static bool Prefix(Attachment __instance) => NavalLabPhysicsPatches.Active?.TickRope(__instance) != false;
    }

    [HarmonyPatch(typeof(Attachment), "UpdateRopeMeshVisualAccordingToTargetPoint")]
    private static class ThrowCurve
    {
        private static void Postfix(Attachment __instance, in Vec3 targetGlobalPosition, float throwingAngleDegree) =>
            NavalLabPhysicsPatches.Active?.ObserveRopeCurve(__instance, targetGlobalPosition, throwingAngleDegree);
    }

    [HarmonyPatch(typeof(Joint), nameof(Joint.OnFixedTick))]
    private static class SolverScope
    {
        private static bool Prefix(Attachment currentAttachment, out NavalLabBehavior __state)
        {
            __state = solver;
            var active = NavalLabPhysicsPatches.Active;
            if (active?.IsFixtureRope(currentAttachment) != true) return true;
            if (!active.BeginRopeJoint(currentAttachment)) return false;
            solver = active;
            return true;
        }
        private static void Finalizer(NavalLabBehavior __state) => solver = __state;
    }

    // Bind the joint's actual force callsites, avoiding the inlined-getter problem seen with oars.
    [HarmonyPatch]
    private static class OwnerEndpointWrites
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var name in new[] { "StabilizeShipUps", "AlignShips", "ApplyConstraintImpulse", "ReduceRelativeDrift" })
                yield return AccessTools.DeclaredMethod(typeof(Joint), name);
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int replaced = 0;
            foreach (var instruction in instructions)
            {
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                    && instruction.operand is MethodInfo method && method.DeclaringType == typeof(NavalPhysics))
                {
                    string wrapper = method.Name == nameof(NavalPhysics.ApplyForceToDynamicBody) ? nameof(Force)
                        : method.Name == nameof(NavalPhysics.ApplyTorque) ? nameof(Torque)
                        : method.Name == nameof(NavalPhysics.ApplyGlobalForceAtLocalPos) ? nameof(ForceAtPosition) : null;
                    if (wrapper != null)
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = AccessTools.DeclaredMethod(typeof(NavalLabRopePatches), wrapper);
                        replaced++;
                    }
                }
                yield return instruction;
            }
            if (replaced < 2) throw new InvalidOperationException("Native rope force callsites changed.");
        }
    }

    private static void Force(NavalPhysics physics, in Vec3 forceVec, ForceMode mode)
    {
        if (solver == null || solver.AllowRopeForce(physics, 0)) physics.ApplyForceToDynamicBody(in forceVec, mode);
    }
    private static void Torque(NavalPhysics physics, in Vec3 torqueVec, ForceMode mode)
    {
        if (solver == null || solver.AllowRopeForce(physics, 1)) physics.ApplyTorque(in torqueVec, mode);
    }
    private static void ForceAtPosition(NavalPhysics physics, in Vec3 localPos, in Vec3 forceVec, ForceMode mode)
    {
        if (solver == null || solver.AllowRopeForce(physics, 2)) physics.ApplyGlobalForceAtLocalPos(in localPos, in forceVec, mode);
    }
}
#endif
