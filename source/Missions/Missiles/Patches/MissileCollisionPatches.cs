using Common.Messaging;
using Common.Util;
using HarmonyLib;
using Missions.Missiles.Handlers;
using Missions.Missiles.Message;
using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

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
