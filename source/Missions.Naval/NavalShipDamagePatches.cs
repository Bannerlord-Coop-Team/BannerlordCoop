using HarmonyLib;
using Missions.Messages;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.ShipActuators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
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
    // Opens the attack scope of the hit.
    [HarmonyPrefix]
    private static void Prefix(Agent attackerAgent, Vec3 impactPosition, out NavalShipDamageGate.HitScope __state) =>
        __state = NavalShipDamageGate.EnterHit(attackerAgent, impactPosition);

    // Closes the attack scope, also when vanilla throws.
    [HarmonyFinalizer]
    private static void Finalizer(NavalShipDamageGate.HitScope __state) => NavalShipDamageGate.ExitHit(__state);
}

// Fire missiles passing through a sail damage it here, outside OnHit.
[HarmonyPatch(typeof(NavalShipsLogic), nameof(NavalShipsLogic.HandleSailsHit))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ShipSailHitScopePatch
{
    // Opens the attack scope of the sail hit.
    [HarmonyPrefix]
    private static void Prefix(Agent attackerAgent, out NavalShipDamageGate.HitScope __state) =>
        __state = NavalShipDamageGate.EnterHit(attackerAgent, Vec3.Invalid);

    // Closes the attack scope, also when vanilla throws.
    [HarmonyFinalizer]
    private static void Finalizer(NavalShipDamageGate.HitScope __state) => NavalShipDamageGate.ExitHit(__state);
}

// Hull HP: ship siege missiles through OnHit, and the capsize and burn ticks, which only the owner's hull counts.
[HarmonyPatch(typeof(MissionShip), nameof(MissionShip.DealDamage))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ShipHullDamagePatch
{
    // Runs vanilla when the gate allows the hit, otherwise reports no damage.
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
    // Opens the collision scope and runs vanilla only when the gate allows the impact.
    [HarmonyPrefix]
    private static bool Prefix(MissionShip __instance, MissionShip hitterShip, bool isRamDamage, Vec3 point, float damage, out bool __state)
    {
        __state = NavalShipDamageGate.EnterCollision(isRamDamage);
        return NavalShipDamageGate.AllowsCollision(__instance, hitterShip, isRamDamage, point, damage);
    }

    // Closes the collision scope, also when vanilla throws.
    [HarmonyFinalizer]
    private static void Finalizer(bool __state) => NavalShipDamageGate.ExitCollision(__state);
}

// Vanilla's ram morale reaches only the rammer team's AI-controlled agents, so each client gives its own agents theirs from
// its own view of the ram (NavalShipDamageGate.AllowsCollision) and the hull owner's routed apply of the hit skips it.
[HarmonyPatch(typeof(NavalAgentMoraleInteractionLogic), nameof(NavalAgentMoraleInteractionLogic.OnShipRammed))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RoutedRamMoralePatch
{
    // Skips vanilla's ram morale inside a routed ram apply.
    [HarmonyPrefix]
    private static bool Prefix() => NavalShipDamageGate.AllowsRamMorale();
}

// Sail HP from weapons and fire missiles; a hit on another owner's sail goes to that owner.
[HarmonyPatch(typeof(MissionShip), nameof(MissionShip.DealDamageToSails))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ShipSailDamagePatch
{
    // Runs vanilla when the gate allows the hit, otherwise reports no damage.
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

// Fire HP from fire hits; a hit on another owner's hull goes to that owner.
[HarmonyPatch(typeof(MissionShip), nameof(MissionShip.DealFireDamage))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ShipFireDamagePatch
{
    // Runs vanilla when the gate allows the hit, otherwise reports no damage.
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
    // Opens the contact scope of the callback's hull.
    [HarmonyPrefix]
    private static void Prefix(MissionShip __instance, WeakGameEntity entity1, out MissionShip __state)
    {
        __state = NavalShipDamageGate.EnterContact(__instance);
#if DEBUG
        NavalContactProbe.CountContact(__instance, entity1);
#endif
    }

    // Closes the contact scope, also when vanilla throws.
    [HarmonyFinalizer]
    private static void Finalizer(MissionShip __state) => NavalShipDamageGate.ExitContact(__state);
}

// The rammer queues the impact on the rammed hull and 0.2 of it on itself; only the rammer's owner keeps either.
[HarmonyPatch(typeof(MissionShip), nameof(MissionShip.QueueShipCollision))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ShipContactCollisionQueuePatch
{
    // Queues the impact only for an own rammer.
    [HarmonyPrefix]
    private static bool Prefix() => NavalShipDamageGate.KeepsContactCollision();
}

// Every entry left in the queue came from this client's rammer, so its owner decides both hulls' damage.
[HarmonyPatch(typeof(MissionShip), nameof(MissionShip.HandleQueuedShipCollisions))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ShipQueuedCollisionScopePatch
{
    // Opens the queued contact scope.
    [HarmonyPrefix]
    private static void Prefix() => NavalShipDamageGate.EnterQueuedContact();

    // Closes the queued contact scope, also when vanilla throws.
    [HarmonyFinalizer]
    private static void Finalizer() => NavalShipDamageGate.ExitQueuedContact();
}

// Vanilla's ram tick runs on every client: ShipCollisionDamagePatch drops a copy rammer's hit, deck parts break locally, and
// the kill loop spares another owner's puppet (NavalRamKills), so each ram death happens once, on its owner.
[HarmonyPatch(typeof(MissionShipRam), nameof(MissionShipRam.OnTick))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RamTickPatch
{
    // Routes the tick's one Agent.Die call through NavalRamKills.DieUnlessPuppet; fails closed when vanilla's tick changed.
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var die = AccessTools.DeclaredMethod(typeof(Agent), nameof(Agent.Die), new[] { typeof(Blow), typeof(Agent.KillInfo) });
        var dieUnlessPuppet = AccessTools.DeclaredMethod(typeof(NavalRamKills), nameof(NavalRamKills.DieUnlessPuppet));
        var result = instructions.ToList();
        int replaced = 0;
        foreach (var instruction in result)
        {
            if (!instruction.Calls(die)) continue;

            instruction.opcode = OpCodes.Call;
            instruction.operand = dieUnlessPuppet;
            replaced++;
        }

        if (replaced != 1) throw new InvalidOperationException("Ram tick kill call changed.");
        return result;
    }
}
