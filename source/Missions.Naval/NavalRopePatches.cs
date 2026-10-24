using HarmonyLib;
using NavalDLC.Missions.NavalPhysics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Attachment = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment;
using Joint = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachmentJoint;
using ForceMode = TaleWorlds.Engine.GameEntityPhysicsExtensions.ForceMode;

namespace Missions.Naval;

// Owners observe every rope they create, whether thrown by a player key, an AI pilot or a plank retarget.
[HarmonyPatch(typeof(ShipAttachmentMachine), nameof(ShipAttachmentMachine.ConnectWithAttachmentPointMachine))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopeConnectPatch
{
    // Refuses a replica's own connect and any rope to a removed hull.
    [HarmonyPrefix]
    private static bool Prefix(ShipAttachmentMachine __instance, ShipAttachmentPointMachine attachmentPointMachine) =>
        NavalRopes.AllowConnection(__instance, attachmentPointMachine);

    // Opens the station's next generation on the owner.
    [HarmonyPostfix]
    private static void Postfix(ShipAttachmentMachine __instance) => NavalRopes.ObserveCreated(__instance);
}

// A replica disconnects only as its owner's state applies.
[HarmonyPatch(typeof(ShipAttachmentMachine), nameof(ShipAttachmentMachine.DisconnectAttachment))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopeDisconnectPatch
{
    // Skips a replica's own disconnect.
    [HarmonyPrefix]
    private static bool Prefix(ShipAttachmentMachine __instance) => NavalRopes.AllowDisconnect(__instance);
}

// A replica's state changes only as its owner's state applies.
[HarmonyPatch(typeof(Attachment), nameof(Attachment.SetAttachmentState))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopeStatePatch
{
    // Skips a replica's own state change.
    [HarmonyPrefix]
    private static bool Prefix(Attachment __instance) => NavalRopes.AllowState(__instance);
}

// Whether a rope becomes a plank is the owner's decision.
[HarmonyPatch(typeof(Attachment), nameof(Attachment.CheckAndConnectBridge))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopePlankEligibilityPatch
{
    // Skips the plank check on a replica.
    [HarmonyPrefix]
    private static bool Prefix(Attachment __instance) => NavalRopes.AllowOwnerDecision(__instance);
}

// Whether a rope breaks is the owner's decision.
[HarmonyPatch(typeof(Attachment), nameof(Attachment.CheckAndBreakAttachment))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopeBreakCheckPatch
{
    // Skips the break check on a replica.
    [HarmonyPrefix]
    private static bool Prefix(Attachment __instance) => NavalRopes.AllowOwnerDecision(__instance);
}

// A replica plank flies on its owner's clock.
[HarmonyPatch(typeof(Attachment), nameof(Attachment.TickThrownBridge))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopeReplicaPlankFlightPatch
{
    // Lays a replica plank out instead of running its flight clock.
    [HarmonyPrefix]
    private static bool Prefix(Attachment __instance) => NavalRopes.TickPlankFlight(__instance);
}

// A replica rope is drawn from its owner's state, and a rope whose end left breaks.
[HarmonyPatch(typeof(Attachment), nameof(Attachment.OnTick))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopeReplicaTickPatch
{
    // Runs vanilla's rope tick only for an owned rope with both ends.
    [HarmonyPrefix]
    private static bool Prefix(Attachment __instance) => NavalRopes.TickRope(__instance);
}

// A joint hooked to a hull that left has no hull to push; its force step is skipped until the rope tick breaks it.
[HarmonyPatch(typeof(Joint), nameof(Joint.OnFixedTick))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopeJointHullGuardPatch
{
    // Runs vanilla's joint step only while both of its hulls are in the mission.
    [HarmonyPrefix]
    private static bool Prefix(Joint __instance) => NavalRopeRemoval.AllowJointStep(__instance);
}

// Vanilla's OnShipRemoved runs on every hull for any removal and breaks every rope and plank in the battle; in co-op it
// breaks only the ropes with an end on the removed hull, so one hull leaving doesn't end every other boarding.
[HarmonyPatch(typeof(MissionShip), nameof(MissionShip.OnShipRemoved))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopeShipRemovalPatch
{
    // Routes both rope loops' CurrentAttachment reads through the removal filter; fails closed when the loops changed.
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var stationGetter = AccessTools.PropertyGetter(typeof(ShipAttachmentMachine), nameof(ShipAttachmentMachine.CurrentAttachment));
        var pointGetter = AccessTools.PropertyGetter(typeof(ShipAttachmentPointMachine), nameof(ShipAttachmentPointMachine.CurrentAttachment));
        var result = new List<CodeInstruction>();
        int stations = 0, points = 0;
        foreach (var instruction in instructions)
        {
            bool isCall = instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt;
            string wrapper = !isCall ? null
                : Equals(instruction.operand, stationGetter) ? nameof(StationRopeBroken)
                : Equals(instruction.operand, pointGetter) ? nameof(PointRopeBroken) : null;
            if (wrapper == null)
            {
                result.Add(instruction);
                continue;
            }

            // The machine is on the stack; the removed hull (argument 1) joins it for the wrapper.
            result.Add(new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction));
            result.Add(new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(typeof(RopeShipRemovalPatch), wrapper)));
            if (wrapper == nameof(StationRopeBroken)) stations++;
            else points++;
        }

        if (stations == 0 || points == 0) throw new InvalidOperationException("Native ship removal rope loops changed.");
        return result;
    }

    // The station's rope when this removal breaks it, else null so vanilla's loop leaves it.
    private static Attachment StationRopeBroken(ShipAttachmentMachine station, MissionShip removed) =>
        BrokenOrNull(station.CurrentAttachment, removed);

    // The point's rope when this removal breaks it, else null so vanilla's loop leaves it.
    private static Attachment PointRopeBroken(ShipAttachmentPointMachine point, MissionShip removed) =>
        BrokenOrNull(point.CurrentAttachment, removed);

    // The rope when it breaks on this removal, else null.
    private static Attachment BrokenOrNull(Attachment rope, MissionShip removed) =>
        rope != null && NavalRopeRemoval.BreaksOnRemoval(rope, removed) ? rope : null;
}

// Retargeting destroys and recreates a rope; on a copied hull that is the owner's decision.
[HarmonyPatch(typeof(ShipAttachmentMachineConnectionLogic), nameof(ShipAttachmentMachineConnectionLogic.OnTick))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopeRetargetPatch
{
    // Skips the retarget logic on a copy.
    [HarmonyPrefix]
    private static bool Prefix(ShipAttachmentMachineConnectionLogic __instance) =>
        NavalRopes.AllowConnectionRetarget(__instance._ownerShip);
}

// The owner's throw curve rides its samples, so a replica draws the same flight.
[HarmonyPatch(typeof(Attachment), nameof(Attachment.UpdateRopeMeshVisualAccordingToTargetPoint))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopeThrowCurvePatch
{
    // Keeps the curve vanilla just drew.
    [HarmonyPostfix]
    private static void Postfix(Attachment __instance, in Vec3 targetGlobalPosition, float throwingAngleDegree) =>
        NavalRopes.ObserveCurve(__instance, targetGlobalPosition, throwingAngleDegree);
}

// A rope joint writes force only to a hull this client simulates; a copy follows its owner's frames. Binds the joint's
// force callsites, because the physics methods can be inlined into them, and runs last so other transpilers keep theirs.
[HarmonyPatch]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopeForcePatch
{
    // [Fixed-tick thread writes] Per force kind: writes let through to own hulls, and writes dropped for copies.
    private static readonly long[] ownForceWrites = new long[3];
    private static readonly long[] foreignForceFiltered = new long[3];

    /// <summary>The force writes let through to own hulls so far, per kind.</summary>
    internal static long[] OwnForceWrites() => ownForceWrites.ToArray();

    /// <summary>The force writes dropped for copies so far, per kind.</summary>
    internal static long[] ForeignForcesFiltered() => foreignForceFiltered.ToArray();

    /// <summary>Resets both counters at mission end.</summary>
    internal static void ClearCounters()
    {
        Array.Clear(ownForceWrites, 0, ownForceWrites.Length);
        Array.Clear(foreignForceFiltered, 0, foreignForceFiltered.Length);
    }

    /// <summary>[Fixed-tick thread] Whether a joint force of this kind may reach the body, counting the write either way.</summary>
    internal static bool AllowForce(NavalPhysics physics, int kind)
    {
        if (!CoopNavalMissionScope.IsActive) return true;

        bool copy = NavalForeignHulls.TryGetCopy(physics, out _);
        Interlocked.Increment(ref (copy ? foreignForceFiltered : ownForceWrites)[kind]);
        return !copy;
    }

    // The joint force steps and the attachment's own force step.
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in new[]
            { nameof(Joint.StabilizeShipUps), nameof(Joint.AlignShips), nameof(Joint.ApplyConstraintImpulse), nameof(Joint.ReduceRelativeDrift) })
            yield return AccessTools.DeclaredMethod(typeof(Joint), name);
        yield return AccessTools.DeclaredMethod(typeof(Attachment), nameof(Attachment.OnFixedTick));
    }

    // Routes every NavalPhysics force write through a gated wrapper; fails closed when the callsites changed.
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

    // ApplyForceToDynamicBody, gated.
    private static void Force(NavalPhysics physics, in Vec3 forceVec, ForceMode mode)
    {
        if (AllowForce(physics, 0)) physics.ApplyForceToDynamicBody(in forceVec, mode);
    }

    // ApplyTorque, gated.
    private static void Torque(NavalPhysics physics, in Vec3 torqueVec, ForceMode mode)
    {
        if (AllowForce(physics, 1)) physics.ApplyTorque(in torqueVec, mode);
    }

    // ApplyGlobalForceAtLocalPos, gated.
    private static void ForceAtPosition(NavalPhysics physics, in Vec3 localPos, in Vec3 forceVec, ForceMode mode)
    {
        if (AllowForce(physics, 2)) physics.ApplyGlobalForceAtLocalPos(in localPos, in forceVec, mode);
    }
}

// Plank decoration randomness and count follow the owner, so a replica plank matches it. Runs last like the force patch.
[HarmonyPatch]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RopePlankCosmeticsPatch
{
    [ThreadStatic] private static Random cosmeticRandom;

    // The three plank methods that draw decoration randomness.
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in new[] { nameof(Attachment.SpawnPlankEntities), nameof(Attachment.ConnectBridge), nameof(Attachment.AddRopesToBridge) })
            yield return AccessTools.DeclaredMethod(typeof(Attachment), name);
    }

    // Seeds this call's randomness, keeping the caller's seed for the finalizer.
    [HarmonyPrefix]
    private static void Prefix(Attachment __instance, MethodBase __originalMethod, out Random __state)
    {
        __state = cosmeticRandom;
        cosmeticRandom = NavalPlankCosmetics.PlankCosmeticRandom(__instance, __originalMethod.Name);
    }

    // Restores the caller's seed, so nested plank calls keep theirs.
    [HarmonyFinalizer]
    private static void Finalizer(Random __state) => cosmeticRandom = __state;

    // Routes MBRandom draws to the seeded wrappers and the plank count to the owner's.
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
                instruction.operand = AccessTools.DeclaredMethod(typeof(NavalPlankCosmetics), nameof(NavalPlankCosmetics.DecorationCount));
            }

            yield return instruction;
        }
    }

    // MBRandom.RandomFloat from the seed while one is set.
    private static float CosmeticFloat() => cosmeticRandom == null ? MBRandom.RandomFloat : (float)cosmeticRandom.NextDouble();

    // MBRandom.RandomInt(max) from the seed while one is set.
    private static int CosmeticInt(int max) => cosmeticRandom == null ? MBRandom.RandomInt(max) : cosmeticRandom.Next(max);

    // MBRandom.RandomInt(min, max) from the seed while one is set.
    private static int CosmeticRange(int min, int max) => cosmeticRandom == null ? MBRandom.RandomInt(min, max) : cosmeticRandom.Next(min, max);
}
