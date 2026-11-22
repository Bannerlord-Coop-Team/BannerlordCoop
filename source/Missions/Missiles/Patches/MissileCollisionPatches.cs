using Common.Messaging;
using Common.Util;
using HarmonyLib;
using Missions.Missiles.Handlers;
using Missions.Missiles.Message;
using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

#if DEBUG
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.ComponentInterfaces;
#endif

namespace Missions.Missiles.Patches;

[HarmonyPatch(typeof(Mission), nameof(Mission.HandleMissileCollisionReaction))]
[HarmonyPatchCategory(MissionModule.MissilePatchCategory)]
public class MissileCollisionPatch
{
    internal readonly struct CollisionState
    {
        public IMissileHandler Handler { get; }
        public Mission.Missile Missile { get; }
        public Guid WorldItemId { get; }

        public CollisionState(IMissileHandler handler, Mission.Missile missile, Guid worldItemId)
        {
            Handler = handler;
            Missile = missile;
            WorldItemId = worldItemId;
        }
    }

    static void Prefix(Mission __instance, int missileIndex, ref Mission.MissileCollisionReaction collisionReaction,
        out CollisionState __state)
    {
        __state = default;
        if (AllowedThread.IsThisThreadAllowed()) return;

        IMissileHandler handler = __instance.GetMissionBehavior<CoopMissionController>()?.MissionComponent.MissileHandler;
        if (handler == null || !__instance._missilesDictionary.TryGetValue(missileIndex, out var missile)) return;

        Guid itemId = handler.PrepareCollision(missile, ref collisionReaction);
        __state = new CollisionState(handler, missile, itemId);
    }

    static void Postfix(CollisionState __state, Mission.MissileCollisionReaction collisionReaction,
        MatrixFrame attachLocalFrame, Agent attachedAgent,
        bool attachedToShield, sbyte attachedBoneIndex, MissionObject attachedMissionObject)
    {
        if (__state.WorldItemId == Guid.Empty) return;

        __state.Handler.ReportCollision(__state.WorldItemId, __state.Missile, collisionReaction,
            attachLocalFrame, attachedAgent, attachedToShield, attachedBoneIndex, attachedMissionObject);
    }
}

[HarmonyPatch(typeof(SpawnedItemEntity), nameof(SpawnedItemEntity.DisableDynamicBody))]
[HarmonyPatchCategory(MissionModule.MissilePatchCategory)]
public class MissilePhysicsSettledPatch
{
    static void Postfix(SpawnedItemEntity __instance)
    {
        if (AllowedThread.IsThisThreadAllowed() || !__instance.IsStuckMissile()) return;

        MessageBroker.Instance.Publish(__instance, new MissilePhysicsSettled(__instance));
    }
}

#if DEBUG
[HarmonyPatch(typeof(Mission), nameof(Mission.MissileHitCallback))]
[HarmonyPatchCategory(MissionModule.MissilePatchCategory)]
public class MissileForcedBouncePatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var decide = AccessTools.Method(typeof(AgentApplyDamageModel), nameof(AgentApplyDamageModel.DecideMissileWeaponFlags));
        var replacement = AccessTools.Method(typeof(MissileForcedBouncePatch), nameof(DecideFlags));
        var parameters = __originalMethod.GetParameters();
        int collision = Array.FindIndex(parameters, value => value.Name == "collisionData") + 1;
        int victim = Array.FindIndex(parameters, value => value.Name == "victim") + 1;
        if (collision == 0 || victim == 0) throw new InvalidOperationException("Failed to bind missile collision arguments.");
        int decisions = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(decide))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction);
                yield return CodeInstruction.LoadArgument(collision);
                yield return CodeInstruction.LoadArgument(victim);
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                decisions++;
            }
            yield return instruction;
        }
        if (decisions != 1) throw new InvalidOperationException($"Failed to bind missile flags decision: {decisions} calls.");
    }

    private static void DecideFlags(AgentApplyDamageModel model, Agent attacker, in MissionWeapon weapon, ref WeaponFlags flags,
        Mission mission, ref AttackCollisionData collisionData, Agent victim)
    {
        model.DecideMissileWeaponFlags(attacker, in weapon, ref flags);
        if (AllowedThread.IsThisThreadAllowed()) return;
        // Change the input before vanilla chooses the frame and calculates the rebound impulse.
        var handler = mission.GetMissionBehavior<CoopMissionController>()?.MissionComponent?.MissileHandler;
        handler?.PrepareForcedBounce(ref collisionData, victim, ref flags);
    }
}
#endif
