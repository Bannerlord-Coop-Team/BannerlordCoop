using Common.Messaging;
using GameInterface;
using HarmonyLib;
using Missions.Agents;
using Missions.Missiles.Message;
using System;
using System.Globalization;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Missiles.Patches;

// #4536 TEMPORARY diagnostics, local-only branch bench/4536-diag, never pushed.

public readonly struct MissileCollisionDiag : IEvent
{
    public bool After { get; }
    public int MissileIndex { get; }
    public Mission.MissileCollisionReaction Reaction { get; }
    public MatrixFrame AttachLocalFrame { get; }
    public bool IsAttachedFrameLocal { get; }
    public Agent AttachedAgent { get; }
    public bool AttachedToShield { get; }
    public sbyte BoneIndex { get; }
    public string AttachedObject { get; }
    public MatrixFrame ResultGlobalFrame { get; }

    public MissileCollisionDiag(bool after, int missileIndex, Mission.MissileCollisionReaction reaction,
        MatrixFrame attachLocalFrame, bool isAttachedFrameLocal, Agent attachedAgent, bool attachedToShield,
        sbyte boneIndex, string attachedObject, MatrixFrame resultGlobalFrame)
    {
        After = after;
        MissileIndex = missileIndex;
        Reaction = reaction;
        AttachLocalFrame = attachLocalFrame;
        IsAttachedFrameLocal = isAttachedFrameLocal;
        AttachedAgent = attachedAgent;
        AttachedToShield = attachedToShield;
        BoneIndex = boneIndex;
        AttachedObject = attachedObject;
        ResultGlobalFrame = resultGlobalFrame;
    }

    public static string Format(MatrixFrame f) => string.Format(CultureInfo.InvariantCulture,
        "{0:R},{1:R},{2:R}|{3:R},{4:R},{5:R}|{6:R},{7:R},{8:R}|{9:R},{10:R},{11:R}",
        f.origin.x, f.origin.y, f.origin.z, f.rotation.s.x, f.rotation.s.y, f.rotation.s.z,
        f.rotation.f.x, f.rotation.f.y, f.rotation.f.z, f.rotation.u.x, f.rotation.u.y, f.rotation.u.z);
}

[HarmonyPatch(typeof(Mission), nameof(Mission.HandleMissileCollisionReaction))]
[HarmonyPatchCategory(MissionModule.MissilePatchCategory)]
public class MissileCollisionDiagPatch
{
    static void Prefix(Mission __instance, int missileIndex, Mission.MissileCollisionReaction collisionReaction,
        MatrixFrame attachLocalFrame, bool isAttachedFrameLocal, Agent attachedAgent, bool attachedToShield,
        sbyte attachedBoneIndex, MissionObject attachedMissionObject)
    {
        string attachedObject = attachedMissionObject == null ? "-"
            : attachedMissionObject.GetType().Name + "#" + attachedMissionObject.Id.Id.ToString(CultureInfo.InvariantCulture);
        MessageBroker.Instance.Publish(__instance, new MissileCollisionDiag(false, missileIndex, collisionReaction,
            attachLocalFrame, isAttachedFrameLocal, attachedAgent, attachedToShield, attachedBoneIndex, attachedObject,
            default));
    }

    static void Postfix(Mission __instance, int missileIndex, Mission.MissileCollisionReaction collisionReaction)
    {
        MatrixFrame global = default;
        if (__instance._missilesDictionary.TryGetValue(missileIndex, out Mission.Missile missile) && missile.Entity != null)
            global = missile.Entity.GetGlobalFrame();
        MessageBroker.Instance.Publish(__instance, new MissileCollisionDiag(true, missileIndex, collisionReaction,
            default, false, null, false, -1, null, global));
    }
}

[HarmonyPatch(typeof(SpawnedItemEntity), nameof(SpawnedItemEntity.DisableDynamicBody))]
[HarmonyPatchCategory(MissionModule.MissilePatchCategory)]
public class MissileSettleDiagPatch
{
    static void Postfix(SpawnedItemEntity __instance)
    {
        if (!__instance.IsStuckMissile() || !ContainerProvider.TryResolve<INetworkWorldItemRegistry>(out var registry)) return;
        registry.TryGetId(__instance, out Guid id);
        Common.Logging.LogManager.GetLogger<MissileSettleDiagPatch>().Information(
            "[MissileDiag] settled worldItem={WorldItemId} ground={Ground} global={Global}",
            id, __instance._groundEntityWhenDisabled?.Name ?? "-",
            MissileCollisionDiag.Format(__instance.GameEntity.GetGlobalFrame()));
    }
}
