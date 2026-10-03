#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NavalDLC.Missions.NavalPhysics;
using NavalDLC.Missions.Objects.UsableMachines;
using TaleWorlds.Engine;
using TaleWorlds.Core;
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
    private static class PlankEligibility
    {
        private static bool Prefix(Attachment __instance, ref bool forceBridge) =>
            NavalLabPhysicsPatches.Active?.AllowPlankCheck(__instance, ref forceBridge) != false;
    }

    [HarmonyPatch(typeof(Attachment), "TickThrownBridge")]
    private static class ReplicaPlankFlight
    {
        private static bool Prefix(Attachment __instance) => NavalLabPhysicsPatches.Active?.TickReplicaPlankFlight(__instance) != false;
    }

    [HarmonyPatch(typeof(Attachment), "CheckAndBreakAttachment")]
    private static class CanonicalBreak
    {
        private static bool Prefix(Attachment __instance) => NavalLabPhysicsPatches.Active?.AllowAttachmentBreakCheck(__instance) != false;
    }

    [HarmonyPatch(typeof(ShipAttachmentMachine), nameof(ShipAttachmentMachine.DisconnectAttachment))]
    private static class EmptyDisconnect
    {
        private static bool Prefix(ShipAttachmentMachine __instance) => NavalLabPhysicsPatches.Active?.AllowPlankRemoval(__instance.CurrentAttachment) != false;
    }

    [HarmonyPatch(typeof(Attachment), nameof(Attachment.Destroy))]
    private static class EmptyDestroy
    {
        private static bool Prefix(Attachment __instance) => NavalLabPhysicsPatches.Active?.AllowPlankRemoval(__instance) != false;
    }

    [HarmonyPatch(typeof(Attachment), nameof(Attachment.OnFixedTick))]
    private static class AttachmentSolverScope
    {
        private static bool Prefix(Attachment __instance, out NavalLabBehavior __state)
        {
            __state = solver;
            var active = NavalLabPhysicsPatches.Active;
            if (active?.IsFixtureRope(__instance) != true) return true;
            if (!active.RopeReady) return false;
            solver = active;
            return true;
        }
        private static void Finalizer(NavalLabBehavior __state) => solver = __state;
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
            yield return AccessTools.DeclaredMethod(typeof(Attachment), nameof(Attachment.OnFixedTick));
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

    [ThreadStatic] private static Random cosmeticRandom;

    [HarmonyPatch]
    private static class PlankCosmetics
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var name in new[] { "SpawnPlankEntities", "ConnectBridge", "AddRopesToBridge" })
                yield return AccessTools.DeclaredMethod(typeof(Attachment), name);
        }
        private static void Prefix(Attachment __instance, MethodBase __originalMethod, out Random __state)
        {
            __state = cosmeticRandom;
            cosmeticRandom = NavalLabPhysicsPatches.Active?.PlankCosmeticRandom(__instance, __originalMethod.Name);
        }
        private static void Finalizer(Random __state) => cosmeticRandom = __state;
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int replaced = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo method && method.DeclaringType == typeof(MBRandom))
                {
                    string wrapper = method.Name == "get_RandomFloat" ? nameof(CosmeticFloat)
                        : method.Name == "RandomInt" && method.GetParameters().Length == 1 ? nameof(CosmeticInt)
                        : method.Name == "RandomInt" && method.GetParameters().Length == 2 ? nameof(CosmeticRange) : null;
                    if (wrapper == null) throw new InvalidOperationException("Plank cosmetic random callsite changed.");
                    instruction.operand = AccessTools.DeclaredMethod(typeof(NavalLabRopePatches), wrapper);
                    replaced++;
                }
                else if (instruction.opcode == OpCodes.Ldfld && instruction.operand is FieldInfo field
                    && field.DeclaringType == typeof(Attachment) && field.Name == "_numberOfPlanksNeeded")
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.DeclaredMethod(typeof(NavalLabRopePatches), nameof(DecorationCount));
                }
                yield return instruction;
            }
            if (replaced == 0) throw new InvalidOperationException("Plank cosmetic random callsites missing.");
        }
    }

    private static float CosmeticFloat() => cosmeticRandom == null ? MBRandom.RandomFloat : (float)cosmeticRandom.NextDouble();
    private static int CosmeticInt(int max) => cosmeticRandom == null ? MBRandom.RandomInt(max) : cosmeticRandom.Next(max);
    private static int CosmeticRange(int min, int max) => cosmeticRandom == null ? MBRandom.RandomInt(min, max) : cosmeticRandom.Next(min, max);
    private static int DecorationCount(Attachment attachment) => NavalLabPhysicsPatches.Active?.PlankDecorationCount(attachment) ?? attachment._numberOfPlanksNeeded;

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
