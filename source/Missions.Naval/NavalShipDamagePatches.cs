using HarmonyLib;
using Missions.Messages;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.ShipActuators;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// Weapons, missiles, ship siege engines and fire reach a hull through OnHit; its attacker decides whose hit it is.
[HarmonyPatch(typeof(MissionShip), nameof(MissionShip.OnHit))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ShipHitScopePatch
{
    [HarmonyPrefix]
    private static void Prefix(Agent attackerAgent, Vec3 impactPosition, out NavalShipDamageGate.HitScope __state) =>
        __state = NavalShipDamageGate.EnterHit(attackerAgent, impactPosition);

    [HarmonyFinalizer]
    private static void Finalizer(NavalShipDamageGate.HitScope __state) => NavalShipDamageGate.ExitHit(__state);
}

// Fire missiles passing through a sail damage it here, outside OnHit.
[HarmonyPatch(typeof(NavalShipsLogic), nameof(NavalShipsLogic.HandleSailsHit))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ShipSailHitScopePatch
{
    [HarmonyPrefix]
    private static void Prefix(Agent attackerAgent, out NavalShipDamageGate.HitScope __state) =>
        __state = NavalShipDamageGate.EnterHit(attackerAgent, Vec3.Invalid);

    [HarmonyFinalizer]
    private static void Finalizer(NavalShipDamageGate.HitScope __state) => NavalShipDamageGate.ExitHit(__state);
}

// Hull HP: ship siege missiles through OnHit, and the capsize and burn ticks, which only the owner's hull counts.
[HarmonyPatch(typeof(MissionShip), nameof(MissionShip.DealDamage))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ShipHullDamagePatch
{
    [HarmonyPrefix]
    private static bool Prefix(MissionShip __instance, float rawDamage, ref float __result, ref int inflictedDamage,
        ref int modifiedDamage, ref DamageTypes damageType, ref bool isFatalDamage)
    {
        if (NavalShipDamageGate.AllowsHit(__instance, BattleShipDamageKind.Hull, rawDamage, 0f, null, -1)) return true;

        __result = 0f;
        inflictedDamage = 0;
        modifiedDamage = 0;
        damageType = DamageTypes.Blunt;
        isFatalDamage = false;
        return false;
    }
}

// Ramming (MissionShipRam) and hull contact (queued by the contact callback) both land here.
[HarmonyPatch(typeof(MissionShip), nameof(MissionShip.DealCollisionDamage))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ShipCollisionDamagePatch
{
    [HarmonyPrefix]
    private static bool Prefix(MissionShip __instance, MissionShip hitterShip, bool isRamDamage, Vec3 point, float damage) =>
        NavalShipDamageGate.AllowsCollision(__instance, hitterShip, isRamDamage, point, damage);
}

[HarmonyPatch(typeof(MissionShip), nameof(MissionShip.DealDamageToSails))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ShipSailDamagePatch
{
    [HarmonyPrefix]
    private static bool Prefix(MissionShip __instance, Agent attackerAgent, float rawDamage, float inflictedDamage,
        MissionSail sailHit, ref float __result)
    {
        if (NavalShipDamageGate.AllowsHit(__instance, BattleShipDamageKind.Sails, rawDamage, inflictedDamage, attackerAgent,
                NavalShipDamageGate.SailIndexOf(__instance, sailHit)))
            return true;

        __result = 0f;
        return false;
    }
}

[HarmonyPatch(typeof(MissionShip), nameof(MissionShip.DealFireDamage))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ShipFireDamagePatch
{
    [HarmonyPrefix]
    private static bool Prefix(MissionShip __instance, float fireDamage, ref float __result)
    {
        if (NavalShipDamageGate.AllowsHit(__instance, BattleShipDamageKind.Fire, fireDamage, 0f, null, -1)) return true;

        __result = 0f;
        return false;
    }
}

// The contact callback's own hull is the rammer of the impacts it queues; it may run on a physics thread.
[HarmonyPatch(typeof(MissionShip), nameof(MissionShip.OnPhysicsCollision))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ShipContactScopePatch
{
    [HarmonyPrefix]
    private static void Prefix(MissionShip __instance, WeakGameEntity entity1, out MissionShip __state)
    {
        __state = NavalShipDamageGate.EnterContact(__instance);
#if DEBUG
        NavalShipDamageGate.LogHullContact(__instance, entity1);
#endif
    }

    [HarmonyFinalizer]
    private static void Finalizer(MissionShip __state) => NavalShipDamageGate.ExitContact(__state);
}

// The rammer queues the impact on the rammed hull and 0.2 of it on itself; only the rammer's owner keeps either.
[HarmonyPatch(typeof(MissionShip), nameof(MissionShip.QueueShipCollision))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ShipContactCollisionQueuePatch
{
    [HarmonyPrefix]
    private static bool Prefix() => NavalShipDamageGate.KeepsContactCollision();
}

// Every entry left in the queue came from this client's rammer, so its owner decides both hulls' damage.
[HarmonyPatch(typeof(MissionShip), nameof(MissionShip.HandleQueuedShipCollisions))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ShipQueuedCollisionScopePatch
{
    [HarmonyPrefix]
    private static void Prefix() => NavalShipDamageGate.EnterQueuedContact();

    [HarmonyFinalizer]
    private static void Finalizer() => NavalShipDamageGate.ExitQueuedContact();
}

// The ram's damage tick deals the hit, breaks the target's deck parts and kills every agent at the impact. Only the
// rammer's owner deals the hit; every client kills only its own agents there, and a copy keeps vanilla's 2 s cooldown.
[HarmonyPatch(typeof(MissionShipRam), nameof(MissionShipRam.OnTick))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class CopyRamTickPatch
{
    [HarmonyPrefix]
    private static bool Prefix(MissionShipRam __instance, out bool __state)
    {
        __state = RunsRamTick(NavalForeignHulls.Contains(__instance._ownerShip));
        if (__state)
        {
            NavalRamKills.EnterRamTick();
            return true;
        }

        ShowCopyRamImpact(__instance);
        return false;
    }

    [HarmonyFinalizer]
    private static void Finalizer(bool __state)
    {
        if (__state) NavalRamKills.ExitRamTick();
    }

    /// <summary>Whether this client runs a ram's damage tick: only for a ram on a hull it simulates.</summary>
    internal static bool RunsRamTick(bool ownerIsCopy) => !ownerIsCopy;

    // Vanilla OnTick without the hit: the cooldown, this client's deck breaks and kills, the effect, then the hit is spent.
    private static void ShowCopyRamImpact(MissionShipRam ram)
    {
        var impact = ram._ramDamageData;
        if (!impact.IsValid) return;

        if (impact.CalculatedDamage > 0f)
        {
            ram._ownerShip.UpdateDamageCooldown(impact.TargetShip);
            NavalRamKills.ApplyCopyImpact(ram);
        }
#if DEBUG
        NavalContactProbe.CountGatedCopyRamTick();
#endif
        ram.TriggerRamCollisionParticleAndSoundEffect(impact.TargetShip.Index, impact.TargetShip.GameEntity, impact.CapsuleData,
            impact.CalculatedDamage);
        ram._ramDamageData = default;
    }
}

// Inside a ram's damage tick another owner's puppet at the impact is spared; its owner kills it on its own copy of the ram.
[HarmonyPatch(typeof(Agent), nameof(Agent.Die))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RamPuppetDiePatch
{
    [HarmonyPrefix]
    private static bool Prefix(Agent __instance) => NavalRamKills.AllowsDie(__instance);
}

#if DEBUG
// Probe: the ram's raycast picks its target here, on a physics thread, before any contact or damage.
[HarmonyPatch(typeof(MissionShipRam), nameof(MissionShipRam.RamCollisionCheckTick))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ShipRamProbePatch
{
    [HarmonyPostfix]
    private static void Postfix(MissionShipRam __instance) =>
        NavalContactProbe.CountRamCheck(__instance._ownerShip, __instance._ramCollisionData.TargetShip,
            __instance._ramCollisionData.HasPoint);
}
#endif
