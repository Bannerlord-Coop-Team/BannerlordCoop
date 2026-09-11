#if DEBUG
using HarmonyLib;
using NavalDLC.Missions.NavalPhysics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Emit;
using System;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

internal static class NavalLabPhysicsPatches
{
    internal static volatile NavalLabBehavior Active;

    private static bool NativeInputAllowed(Mission mission) => Active == null || !Active.HasNativeViews
        || Active.Mission != mission || Active.CanUseNativeInput;

    [HarmonyPatch(typeof(TaleWorlds.MountAndBlade.View.MissionViews.MissionMainAgentController), "OnPreMissionTick")]
    private static class SingleMainAgentInput
    {
        private static bool Prefix(MissionBehavior __instance) => NativeInputAllowed(__instance.Mission);
    }

    [HarmonyPatch(typeof(NavalDLC.View.MissionViews.MissionShipControlView), "HandleShipControls")]
    private static class SingleShipAxes
    {
        private static bool Prefix(NavalDLC.View.MissionViews.MissionShipControlView __instance)
        {
            var active = Active;
            if (active?.IsTwoClientNative == true && active.Mission == __instance.Mission)
            {
                try { active.RouteNativeAxes(__instance); }
                catch (Exception exception) { active.Reject("native.input_failed:" + exception); }
                return false;
            }
            return NativeInputAllowed(__instance.Mission);
        }
    }

    [HarmonyPatch(typeof(NavalDLC.GauntletUI.MissionViews.MissionGauntletShipControlView), "TickInput")]
    private static class SingleShipKeys
    {
        private static bool Prefix(MissionBehavior __instance) => NativeInputAllowed(__instance.Mission);
    }

    [HarmonyPatch(typeof(NavalDLC.GauntletUI.MissionViews.MissionGauntletNavalOrderUIHandler), "TickInput")]
    private static class SingleOrderKeys
    {
        private static bool Prefix(NavalDLC.GauntletUI.MissionViews.MissionGauntletNavalOrderUIHandler __instance)
        {
            var active = Active;
            if (active?.IsTwoClientNative != true || active.Mission != __instance.Mission) return NativeInputAllowed(__instance.Mission);
            __instance._isReceivingInput = false;
            __instance._dataSource?.UpdateCanUseShortcuts(false);
            __instance._dataSource?.TryCloseToggleOrder();
            __instance.SuspendView();
            return false;
        }
    }

    [HarmonyPatch(typeof(Agent), nameof(Agent.HandleStartUsingAction))]
    private static class SingleStationInput
    {
        private static bool Prefix(Agent __instance, UsableMissionObject targetObject)
        {
            var active = Active;
            if (active == null || !active.HasNativeViews || !active.Agents.Contains(__instance)) return true;
            return active.CanUseNativeInput && active.LocalCaptain == __instance
                && active.LocalShip.ShipControllerMachine.PilotStandingPoint == targetObject;
        }
    }

    [HarmonyPatch(typeof(NavalDLC.Missions.AI.TeamAI.NavalOrderController))]
    private static class SingleOrders
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods() => new[]
        {
            "SetOrder", "SetOrderWithPosition", "SetOrderWithTwoPositions", "SetOrderWithFormation", "SetOrderWithAgent"
        }.Select(name => AccessTools.DeclaredMethod(typeof(NavalDLC.Missions.AI.TeamAI.NavalOrderController), name));

        private static bool Prefix(OrderController __instance, OrderType orderType)
        {
            var active = Active;
            if (active == null || !active.HasNativeViews || !active.Mission.Teams.Contains(__instance.Team)) return true;
            if (active.IsTwoClientNative) return false;
            if (!active.CanUseNativeControls || __instance != active.Mission.PlayerTeam.PlayerOrderController
                || __instance.SelectedFormations.Count != 1 || __instance.SelectedFormations[0] != active.Ships[0].Formation) return false;
            // No opponent, retreat/removal, boarding or transfer fixture exists in this mode.
            return orderType == OrderType.Move || orderType == OrderType.StandYourGround
                || orderType == OrderType.Mount
                || orderType == OrderType.AIControlOn || orderType == OrderType.AIControlOff
                || orderType == OrderType.HoldFire || orderType == OrderType.FireAtWill;
        }
    }

    [HarmonyPatch]
    private static class SingleUnsupportedOrders
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(NavalDLC.Missions.AI.TeamAI.NavalOrderController), "SetOrderWithOrderableObject");
            foreach (var name in new[] { "SetOrderWithFormationAndPercentage", "SetOrderWithFormationAndNumber",
                "TransferUnitWithPriorityFunction", "RearrangeFormationsAccordingToFilters" })
                yield return AccessTools.DeclaredMethod(typeof(OrderController), name);
        }

        private static bool Prefix(OrderController __instance)
        {
            var active = Active;
            return active == null || !active.HasNativeViews || !active.Mission.Teams.Contains(__instance.Team);
        }
    }

    [HarmonyPatch(typeof(NavalDLC.Missions.MissionLogics.NavalShipsLogic), "OnEndMission")]
    private static class SingleMissionEndHold
    {
        private static void Prefix(MissionBehavior __instance)
        {
            var active = Active;
            // NavalShipsLogic removes its hulls before the later co-op controller leaves.
            if (active == null || (!active.IsSingleClientNative && !active.IsFactoryProbe) || active.Mission != __instance.Mission) return;
            try { active.Hold(); }
            catch (Exception exception) { active.Reject("shutdown.hold_failed:" + exception.GetType().FullName); }
        }
    }

    [HarmonyPatch(typeof(Team), "OrderController_OnOrderIssued")]
    private static class SingleOrderObservation
    {
        private static void Prefix(Team __instance, OrderType orderType)
        {
            var active = Active;
            if (active?.IsSingleClientNative == true && active.Mission.PlayerTeam == __instance)
                active.ObserveNativeOrder(orderType);
        }
    }

    [HarmonyPatch(typeof(Agent), nameof(Agent.UseGameObject))]
    private static class HeldUseTrace
    {
        private static void Prefix(Agent __instance, UsableMissionObject usedObject, out NavalLabBehavior.HelmTraceCall __state)
        {
            __state = null;
            try { __state = Active?.BeginHelmTrace(__instance, usedObject, "use"); }
            catch { }
        }

        private static void Finalizer(NavalLabBehavior.HelmTraceCall __state, Exception __exception)
        {
            try { NavalLabBehavior.EndHelmTrace(__state, __exception); }
            catch { }
        }
    }

    [HarmonyPatch(typeof(Agent), "StopUsingGameObjectAux")]
    private static class HeldStopTrace
    {
        private static void Prefix(Agent __instance, out NavalLabBehavior.HelmTraceCall __state)
        {
            __state = null;
            try { __state = Active?.BeginHelmTrace(__instance, __instance.CurrentlyUsedGameObject, "stop"); }
            catch { }
        }

        private static void Finalizer(NavalLabBehavior.HelmTraceCall __state, Exception __exception)
        {
            try { NavalLabBehavior.EndHelmTrace(__state, __exception); }
            catch { }
        }
    }

    [HarmonyPatch(typeof(ShipControllerMachine), "OnTick")]
    private static class HeldHelmTick
    {
        private static void Prefix(ShipControllerMachine __instance) => Active?.BeforeHeldHelmTick(__instance);
    }

    [HarmonyPatch(typeof(ShipControllerMachine), "OnTick")]
    private static class HeldCaptureBranch
    {
        private static bool SuppressCapture(ShipControllerMachine machine) => Active?.SuppressHeldCapture(machine) == true;

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var code = instructions.ToList();
            var pilot = AccessTools.PropertyGetter(typeof(UsableMachine), nameof(UsableMachine.PilotAgent));
            var main = AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.IsMainAgent));
            var vacant = AccessTools.Method(typeof(ShipControllerMachine), nameof(ShipControllerMachine.IsAttachedShipVacant));
            var formation = AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.Formation));
            var starts = Enumerable.Range(0, Math.Max(0, code.Count - 11)).Where(i =>
                code[i].opcode == OpCodes.Ldarg_0 && code[i + 1].Calls(pilot) && code[i + 2].Calls(main)
                && code[i + 3].opcode == OpCodes.Brfalse && code[i + 4].opcode == OpCodes.Ldarg_0
                && code[i + 5].Calls(vacant) && code[i + 6].opcode == OpCodes.Brfalse
                && code[i + 7].opcode == OpCodes.Ldarg_0 && code[i + 8].Calls(pilot)
                && code[i + 9].Calls(formation) && code[i + 10].opcode == OpCodes.Brfalse).ToArray();
            if (starts.Length != 1 || code.Any(instruction => instruction.blocks.Count != 0)) throw UnsupportedShape();
            int entry = starts[0] + 11;
            var noncapture = (Label)code[entry - 1].operand;
            int end = code.FindIndex(instruction => instruction.labels.Contains(noncapture));
            if (end <= entry || !code[end].labels.Contains((Label)code[starts[0] + 3].operand)
                || !code[end].labels.Contains((Label)code[starts[0] + 6].operand) || code[end - 1].opcode != OpCodes.Ret || code[entry].opcode != OpCodes.Ldarg_0
                || !code[entry + 1].LoadsField(AccessTools.Field(typeof(ShipControllerMachine), "_navalShipsLogic")))
                throw UnsupportedShape();
            var branch = code.GetRange(entry, end - entry);
            var expectedCalls = new[]
            {
                pilot, formation, AccessTools.PropertyGetter(typeof(Team), nameof(Team.TeamSide)), pilot, formation,
                AccessTools.Method(typeof(NavalDLC.Missions.MissionLogics.NavalShipsLogic), "GetShipAssignment",
                    new[] { typeof(TeamSideEnum), typeof(FormationClass) }),
                AccessTools.PropertyGetter(typeof(NavalDLC.ShipAssignment), "MissionShip"),
                AccessTools.PropertyGetter(typeof(ShipControllerMachine), nameof(ShipControllerMachine.AttachedShip)),
                AccessTools.Method(typeof(MissionShip), nameof(MissionShip.AreShipsConnected)), pilot,
                AccessTools.Method(typeof(Agent), nameof(Agent.SetActionChannel)), pilot, pilot,
                AccessTools.Method(typeof(Agent), nameof(Agent.StopUsingGameObject)),
                AccessTools.Method(typeof(ShipControllerMachine), "OnShipCapturedByAgent"),
                AccessTools.Method(typeof(MissionShip), nameof(MissionShip.InvalidateActiveFormationTroopOnShipCache)),
                AccessTools.PropertyGetter(typeof(ShipControllerMachine), nameof(ShipControllerMachine.AttachedShip)),
                AccessTools.Method(typeof(MissionShip), nameof(MissionShip.InvalidateActiveFormationTroopOnShipCache)), pilot,
                AccessTools.Method(typeof(Agent), nameof(Agent.StopUsingGameObject))
            };
            if (!branch.Where(instruction => instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                .Select(instruction => instruction.operand).SequenceEqual(expectedCalls)) throw UnsupportedShape();
            // No outside branch may enter after the barrier, and capture exits must already be returns.
            var insideLabels = branch.Skip(1).SelectMany(instruction => instruction.labels).ToArray();
            if (code.Take(entry).Concat(code.Skip(end)).Any(instruction => instruction.operand is Label label && insideLabels.Contains(label))
                || code.Any(instruction => instruction.operand is Label[])) throw UnsupportedShape();
            foreach (var instruction in branch.Where(instruction => instruction.operand is Label))
            {
                int target = code.FindIndex(candidate => candidate.labels.Contains((Label)instruction.operand));
                if (target < entry || (target >= end && code[target].opcode != OpCodes.Ret)) throw UnsupportedShape();
            }
            var exit = generator.DefineLabel();
            code[end - 1].labels.Add(exit);
            var load = new CodeInstruction(OpCodes.Ldarg_0);
            load.labels.AddRange(code[entry].labels);
            code[entry].labels.Clear();
            code.InsertRange(entry, new[]
            {
                load, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(HeldCaptureBranch), nameof(SuppressCapture))),
                new CodeInstruction(OpCodes.Brtrue, exit)
            });
            return code;
        }

        private static InvalidOperationException UnsupportedShape() =>
            new InvalidOperationException("Unsupported native helm capture branch; naval fixture cannot open.");
    }

    [HarmonyPatch(typeof(MissionShip), nameof(MissionShip.InitForMission))]
    private static class ShipInitialization
    {
        private static void Prefix(NavalDLC.Missions.MissionLogics.NavalShipsLogic shipsLogic)
        {
            var active = Active;
            if (active != null && active.Mission == shipsLogic.Mission)
                active.RecordStartup("init_for_mission_begin");
        }

        private static Exception Finalizer(MissionShip __instance, NavalDLC.Missions.MissionLogics.NavalShipsLogic shipsLogic, Exception __exception)
        {
            var active = Active;
            if (active != null && active.Mission == shipsLogic.Mission)
            {
                if (active.IsFactoryProbe)
                {
                    if (__exception != null)
                    {
                        active.Reject(__exception.ToString());
                        return __exception;
                    }
                    try { active.CompleteFactoryHull(__instance); }
                    catch (Exception exception) { active.Reject(exception.ToString()); return exception; }
                }
                if (__exception != null) active.RecordInitializationFailure(__exception);
                else active.RecordStartup("init_for_mission_complete");
            }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(NavalPhysics), "OnFixedTick")]
    private static class FixedTick
    {
        private static void Prefix(NavalPhysics __instance)
        {
            var active = Active;
            if (active?.IsFactoryProbe == true) { active.ObserveFactoryFixedTick(__instance, parallel: false); return; }
            if (active == null || !active.Ships.Any(ship => ship?.Physics == __instance)) return;
            Interlocked.Increment(ref active.FixedTicks);
            if (__instance.GameEntity.HasDynamicRigidBodyAndActiveSimulation())
                Interlocked.Increment(ref active.ActiveFixedTicks);
        }
    }

    [HarmonyPatch(typeof(NavalPhysics), "OnParallelFixedTick")]
    private static class FactoryParallelFixedTick
    {
        private static void Prefix(NavalPhysics __instance) => Active?.ObserveFactoryFixedTick(__instance, parallel: true);
    }

    [HarmonyPatch(typeof(NavalPhysics), nameof(NavalPhysics.ApplyForceToDynamicBody))]
    private static class AppliedForce
    {
        private static void Prefix(NavalPhysics __instance)
        {
            var active = Active;
            if (active?.IsFactoryProbe == true) { active.ObserveFactoryForce(); return; }
            if (active != null && active.Ships.Any(ship => ship?.Physics == __instance))
                Interlocked.Increment(ref active.ForceApplications);
        }
    }

    [HarmonyPatch(typeof(Agent), nameof(Agent.Health), MethodType.Setter)]
    private static class AgentHealth
    {
        private static bool Prefix(Agent __instance, float value)
        {
            if (__instance.Origin is not NavalLabAgentOrigin || value >= __instance.Health) return true;
            Active?.Reject("agent.damageAttempt");
            return false;
        }
    }

    [HarmonyPatch(typeof(MissionShip), nameof(MissionShip.SetSinkingState))]
    private static class ShipSinking
    {
        private static bool Prefix(MissionShip __instance, NavalPhysics.SinkingState state)
        {
            var active = Active;
            if (active == null || !active.Ships.Contains(__instance) || state == NavalPhysics.SinkingState.Floating) return true;
            active.Reject("ship.sinkingAttempt");
            return false;
        }
    }

    [HarmonyPatch(typeof(MissionShip), nameof(MissionShip.DealDamage))]
    private static class ShipDamage
    {
        private static bool Prefix(MissionShip __instance, ref float __result,
            out int inflictedDamage, out int modifiedDamage, out DamageTypes damageType, out bool isFatalDamage)
        {
            inflictedDamage = 0;
            modifiedDamage = 0;
            damageType = default;
            isFatalDamage = false;
            var active = Active;
            if (active == null || !active.Ships.Contains(__instance)) return true;
            __result = 0;
            active.Reject("ship.damageAttempt");
            return false;
        }
    }
}
#endif
