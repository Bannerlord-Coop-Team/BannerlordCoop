using HarmonyLib;
using NavalDLC.Missions.NavalPhysics;
using NavalDLC.Missions.Objects.UsableMachines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Attachment = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment;
using ForceMode = TaleWorlds.Engine.GameEntityPhysicsExtensions.ForceMode;

namespace Missions.Naval;

// Owners observe every rope they create, whether thrown by a player key, an AI pilot or a plank retarget.
[HarmonyPatch(typeof(ShipAttachmentMachine), nameof(ShipAttachmentMachine.ConnectWithAttachmentPointMachine))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopeConnectPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ShipAttachmentMachine __instance) => NavalRopes.AllowConnection(__instance);

    [HarmonyPostfix]
    private static void Postfix(ShipAttachmentMachine __instance) => NavalRopes.ObserveCreated(__instance);
}

[HarmonyPatch(typeof(ShipAttachmentMachine), nameof(ShipAttachmentMachine.DisconnectAttachment))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopeDisconnectPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ShipAttachmentMachine __instance) => NavalRopes.AllowDisconnect(__instance);
}

[HarmonyPatch(typeof(Attachment), nameof(Attachment.SetAttachmentState))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopeStatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(Attachment __instance) => NavalRopes.AllowState(__instance);
}

[HarmonyPatch(typeof(Attachment), nameof(Attachment.CheckAndConnectBridge))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopePlankEligibilityPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Attachment __instance) => NavalRopes.AllowOwnerDecision(__instance);
}

[HarmonyPatch(typeof(Attachment), "CheckAndBreakAttachment")]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopeBreakCheckPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Attachment __instance) => NavalRopes.AllowOwnerDecision(__instance);
}

[HarmonyPatch(typeof(Attachment), "TickThrownBridge")]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopeReplicaPlankFlightPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Attachment __instance) => NavalRopes.TickPlankFlight(__instance);
}

[HarmonyPatch(typeof(Attachment), nameof(Attachment.OnTick))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopeReplicaTickPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Attachment __instance) => NavalRopes.TickRope(__instance);
}

// Retargeting destroys and recreates a rope; on a copied hull that is the owner's decision.
[HarmonyPatch(typeof(ShipAttachmentMachineConnectionLogic), "OnTick")]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopeRetargetPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ShipAttachmentMachineConnectionLogic __instance) =>
        NavalRopes.AllowConnectionRetarget(__instance._ownerShip);
}

[HarmonyPatch(typeof(Attachment), "UpdateRopeMeshVisualAccordingToTargetPoint")]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopeThrowCurvePatch
{
    [HarmonyPostfix]
    private static void Postfix(Attachment __instance, in Vec3 targetGlobalPosition, float throwingAngleDegree) =>
        NavalRopes.ObserveCurve(__instance, targetGlobalPosition, throwingAngleDegree);
}

// The joint's force callsites, bound directly because the physics methods can be inlined into them. Runs last, so
// another transpiler over the same callsites keeps its wrappers.
[HarmonyPatch]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopeForcePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in new[] { "StabilizeShipUps", "AlignShips", "ApplyConstraintImpulse", "ReduceRelativeDrift" })
            yield return AccessTools.DeclaredMethod(typeof(ShipAttachmentMachine.ShipAttachmentJoint), name);
        yield return AccessTools.DeclaredMethod(typeof(Attachment), nameof(Attachment.OnFixedTick));
    }

    [HarmonyTranspiler]
    [HarmonyPriority(Priority.Last)]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var result = instructions.ToList();
        int routed = 0;
        foreach (var instruction in result)
        {
            if ((instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt) || instruction.operand is not MethodInfo method)
                continue;

            if (method.DeclaringType?.Assembly == typeof(RopeForcePatch).Assembly) routed++;
            if (method.DeclaringType != typeof(NavalPhysics)) continue;

            string wrapper = method.Name == nameof(NavalPhysics.ApplyForceToDynamicBody) ? nameof(Force)
                : method.Name == nameof(NavalPhysics.ApplyTorque) ? nameof(Torque)
                : method.Name == nameof(NavalPhysics.ApplyGlobalForceAtLocalPos) ? nameof(ForceAtPosition) : null;
            if (wrapper == null) continue;

            instruction.opcode = OpCodes.Call;
            instruction.operand = AccessTools.DeclaredMethod(typeof(RopeForcePatch), wrapper);
            routed++;
        }

        if (routed == 0) throw new InvalidOperationException("Native rope force callsites changed.");
        return result;
    }

    private static void Force(NavalPhysics physics, in Vec3 forceVec, ForceMode mode)
    {
        if (NavalRopes.AllowForce(physics, 0)) physics.ApplyForceToDynamicBody(in forceVec, mode);
    }

    private static void Torque(NavalPhysics physics, in Vec3 torqueVec, ForceMode mode)
    {
        if (NavalRopes.AllowForce(physics, 1)) physics.ApplyTorque(in torqueVec, mode);
    }

    private static void ForceAtPosition(NavalPhysics physics, in Vec3 localPos, in Vec3 forceVec, ForceMode mode)
    {
        if (NavalRopes.AllowForce(physics, 2)) physics.ApplyGlobalForceAtLocalPos(in localPos, in forceVec, mode);
    }
}

// Plank decoration randomness and count follow the owner, so a replica plank matches it. Runs last like the force patch.
[HarmonyPatch]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopePlankCosmeticsPatch
{
    [ThreadStatic] private static Random cosmeticRandom;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in new[] { "SpawnPlankEntities", "ConnectBridge", "AddRopesToBridge" })
            yield return AccessTools.DeclaredMethod(typeof(Attachment), name);
    }

    [HarmonyPrefix]
    private static void Prefix(Attachment __instance, MethodBase __originalMethod, out Random __state)
    {
        __state = cosmeticRandom;
        cosmeticRandom = NavalRopes.PlankCosmeticRandom(__instance, __originalMethod.Name);
    }

    [HarmonyFinalizer]
    private static void Finalizer(Random __state) => cosmeticRandom = __state;

    [HarmonyTranspiler]
    [HarmonyPriority(Priority.Last)]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo method && method.DeclaringType == typeof(MBRandom))
            {
                string wrapper = method.Name == "get_RandomFloat" ? nameof(CosmeticFloat)
                    : method.Name == nameof(MBRandom.RandomInt) && method.GetParameters().Length == 1 ? nameof(CosmeticInt)
                    : method.Name == nameof(MBRandom.RandomInt) && method.GetParameters().Length == 2 ? nameof(CosmeticRange) : null;
                if (wrapper != null) instruction.operand = AccessTools.DeclaredMethod(typeof(RopePlankCosmeticsPatch), wrapper);
            }
            else if (instruction.opcode == OpCodes.Ldfld && instruction.operand is FieldInfo field
                && field.DeclaringType == typeof(Attachment) && field.Name == nameof(Attachment._numberOfPlanksNeeded))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.DeclaredMethod(typeof(NavalRopes), nameof(NavalRopes.DecorationCount));
            }

            yield return instruction;
        }
    }

    private static float CosmeticFloat() => cosmeticRandom == null ? MBRandom.RandomFloat : (float)cosmeticRandom.NextDouble();

    private static int CosmeticInt(int max) => cosmeticRandom == null ? MBRandom.RandomInt(max) : cosmeticRandom.Next(max);

    private static int CosmeticRange(int min, int max) => cosmeticRandom == null ? MBRandom.RandomInt(min, max) : cosmeticRandom.Next(min, max);
}
