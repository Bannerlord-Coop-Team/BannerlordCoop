using Common.Messaging;
using Common.Util;
using HarmonyLib;
using Missions.Missiles.Message;
using TaleWorlds.MountAndBlade;

namespace Missions.Missiles.Patches;

/// <summary>
/// Reports the world item a stuck missile becomes, so the missile handler can give it the shot's shared identity.
/// </summary>
[HarmonyPatch(typeof(Mission), nameof(Mission.SpawnWeaponAsDropFromMissile))]
[HarmonyPatchCategory(MissionModule.WeaponDropPatchCategory)]
public class MissileItemDropPatch
{
    static void Prefix(Mission __instance, int missileIndex, out Mission.Missile __state)
    {
        __state = null;
        if (AllowedThread.IsThisThreadAllowed()) return;

        __instance._missilesDictionary?.TryGetValue(missileIndex, out __state);
    }

    static void Postfix(Mission __instance, int missileIndex, Mission.Missile __state)
    {
        SpawnedItemEntity item = __state?.Entity?.GetFirstScriptOfType<SpawnedItemEntity>();
        if (item == null) return;

        MessageBroker.Instance.Publish(__instance, new MissileItemDropped(missileIndex, __state.ShooterAgent, item));
    }
}
