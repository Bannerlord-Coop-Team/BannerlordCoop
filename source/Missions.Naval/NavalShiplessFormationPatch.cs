using Common.Logging;
using HarmonyLib;
using NavalDLC.Missions;
using NavalDLC.Missions.AI.Behaviors;
using NavalDLC.Missions.AI.TeamAI;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.ShipControl;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// Makes vanilla's three unchecked formation ship lookups skip a formation without a ship, as their sibling queries do (a
/// capture leaves the prize's old formation shipless while its swimmers stay in it).
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class NavalShiplessFormationPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalShiplessFormationPatch>();

    private static readonly MethodInfo GetShip = AccessTools.Method(typeof(NavalShipsLogic), nameof(NavalShipsLogic.GetShip),
        new[] { typeof(TaleWorlds.Core.TeamSideEnum), typeof(TaleWorlds.Core.FormationClass), typeof(NavalDLC.Missions.Objects.MissionShip).MakeByRefType() });

    // Patches only while vanilla still has an unchecked lookup, so a game update that fixes it leaves nothing to patch (logged).
    [HarmonyPrepare]
    private static bool Prepare()
    {
        if (TargetMethods().Any()) return true;

        Logger.Warning("[NavalSync] No unchecked formation ship lookup found in NavalQuerySystem or ShipOrder; the shipless formation patch is off");
        return false;
    }

    /// <summary>
    /// The query lambdas of NavalQuerySystem (average own and enemy ship position) and ShipOrder (closest enemy ship) that
    /// discard GetShip's result and read the ship anyway.
    /// </summary>
    internal static IEnumerable<MethodBase> TargetMethods() =>
        new[] { typeof(NavalQuerySystem), typeof(ShipOrder) }
            .SelectMany(type => type.GetMethods(AccessTools.allDeclared))
            .Where(method => method.Name.StartsWith("<", StringComparison.Ordinal) && HasUncheckedLookup(method));

    // Whether the method's body calls GetShip and pops its result.
    private static bool HasUncheckedLookup(MethodBase method)
    {
        var body = PatchProcessor.GetOriginalInstructions(method);
        for (int i = 0; i + 1 < body.Count; i++)
        {
            if (IsUncheckedLookup(body, i)) return true;
        }

        return false;
    }

    // A GetShip call whose bool result the next instruction discards.
    private static bool IsUncheckedLookup(IList<CodeInstruction> body, int index) =>
        body[index].Calls(GetShip) && body[index + 1].opcode == OpCodes.Pop;

    /// <summary>Turns each <c>GetShip(...); ship.X</c> into <c>if (!GetShip(...)) continue;</c> of its formation loop.</summary>
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var body = instructions.ToList();
        int guarded = 0;
        for (int i = 0; i + 1 < body.Count; i++)
        {
            if (!IsUncheckedLookup(body, i)) continue;

            var next = LoopConditionAfter(body, i);
            if (next == null) continue;

            body[i + 1].opcode = OpCodes.Brfalse;
            body[i + 1].operand = LabelOf(next, generator);
            guarded++;
        }

        if (guarded == 0) throw new InvalidOperationException("Native ship lookups changed.");
        return body;
    }

    // The start of the enclosing foreach's condition (ldloca enumerator; call MoveNext), the first one after the lookup.
    private static CodeInstruction LoopConditionAfter(IList<CodeInstruction> body, int index)
    {
        for (int i = index + 2; i < body.Count; i++)
        {
            if (body[i].operand is MethodInfo method && method.Name == nameof(IEnumerator<object>.MoveNext)) return body[i - 1];
        }

        return null;
    }

    // The instruction's first label, added when it has none.
    private static Label LabelOf(CodeInstruction instruction, ILGenerator generator)
    {
        if (instruction.labels.Count > 0) return instruction.labels[0];

        var label = generator.DefineLabel();
        instruction.labels.Add(label);
        return label;
    }
}

/// <summary>
/// The naval behaviours that read their formation's hull, or the closest enemy formation's hull, in GetAiWeight weigh nothing
/// in a co-op naval battle while that hull is missing: a capture, a sinking hull's departure or a removal can leave a
/// formation with units (swimmers) and no hull, and the async team tick evaluates these weights every formation AI tick.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ShiplessFormationBehaviorWeightPatch
{
    // The behaviours whose weight dereferences the formation's hull (_formationMainShip) unchecked.
    internal static IEnumerable<MethodBase> TargetMethods() =>
        new[] { typeof(BehaviorNavalRemoveConnection), typeof(BehaviorNavalRamming), typeof(BehaviorNavalSkirmish) }
            .Select(type => AccessTools.DeclaredMethod(type, "GetAiWeight"));

    // Answers 0 instead of dereferencing a missing hull; a behaviour whose cached hull is null while its formation has one
    // again (a hull joined it) re-reads it, as vanilla's own refresh does.
    [HarmonyPrefix]
    internal static bool Prefix(BehaviorComponent __instance, ref float __result)
    {
        if (!CoopNavalMissionScope.IsActive) return true;

        var shipsLogic = NavalShiplessFormations.ShipsLogic;
        var ship = NavalShiplessFormations.ShipOf(shipsLogic, __instance.Formation, out bool known);
        bool enemyHasShip = __instance is not BehaviorNavalRamming
            || NavalShiplessFormations.ClosestEnemyHasShip(shipsLogic, __instance.Formation);
        switch (Decide(known, ship != null, MainShipOf(__instance) != null, enemyHasShip))
        {
            case WeightCall.Vanilla:
                return true;
            case WeightCall.ReReadThenVanilla:
                SetMainShip(__instance, ship);
                return true;
            default:
                __result = 0f;
                return false;
        }
    }

    /// <summary>
    /// What the weight prefix does: 0 for a formation known to have no hull, for a ramming target formation without a hull, or
    /// for a missing cached hull it can't re-read; a re-read when the formation has a hull again; vanilla otherwise.
    /// </summary>
    internal static WeightCall Decide(bool known, bool formationHasShip, bool hasCachedShip, bool enemyHasShip)
    {
        if (known && !formationHasShip) return WeightCall.Weightless;
        if (!enemyHasShip) return WeightCall.Weightless;
        if (hasCachedShip) return WeightCall.Vanilla;
        return known ? WeightCall.ReReadThenVanilla : WeightCall.Weightless;
    }

    /// <summary>The hull a patched behaviour cached for its formation, which vanilla's refresh leaves null after a capture.</summary>
    internal static MissionShip MainShipOf(BehaviorComponent behavior) => behavior switch
    {
        BehaviorNavalRemoveConnection removeConnection => removeConnection._formationMainShip,
        BehaviorNavalRamming ramming => ramming._formationMainShip,
        BehaviorNavalSkirmish skirmish => skirmish._formationMainShip,
        _ => null,
    };

    // Re-reads the formation's hull into a patched behaviour, before the vanilla weight runs on it.
    private static void SetMainShip(BehaviorComponent behavior, MissionShip ship)
    {
        if (behavior is BehaviorNavalRemoveConnection removeConnection) removeConnection._formationMainShip = ship;
        else if (behavior is BehaviorNavalRamming ramming) ramming._formationMainShip = ship;
        else if (behavior is BehaviorNavalSkirmish skirmish) skirmish._formationMainShip = ship;
    }

    /// <summary>What the weight prefix does with one evaluation.</summary>
    internal enum WeightCall
    {
        Vanilla,
        ReReadThenVanilla,
        Weightless,
    }
}

/// <summary>
/// Ramming and skirmish set ship orders toward the closest enemy formation's hull unchecked on the async team tick; in a co-op
/// naval battle they skip while their own or that enemy formation has no hull (L6 swimmers are such an enemy formation).
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ShiplessTargetShipOrdersPatch
{
    // The two private order setters that read both hulls.
    internal static IEnumerable<MethodBase> TargetMethods() =>
        new[] { typeof(BehaviorNavalRamming), typeof(BehaviorNavalSkirmish) }
            .Select(type => AccessTools.DeclaredMethod(type, "CalculateAndSetShipOrders"));

    // Runs vanilla only with both hulls present, or outside a co-op battle.
    [HarmonyPrefix]
    internal static bool Prefix(BehaviorComponent __instance)
    {
        if (!CoopNavalMissionScope.IsActive) return true;

        return ShiplessFormationBehaviorWeightPatch.MainShipOf(__instance) != null
            && NavalShiplessFormations.ClosestEnemyHasShip(NavalShiplessFormations.ShipsLogic, __instance.Formation);
    }
}

/// <summary>
/// A skirmish order at a formation without a hull (the player's order controller targeting swimmers, or a behaviour) would
/// dereference the missing target in UpdateSkirmishOrder; in a co-op naval battle the order is not given.
/// </summary>
[HarmonyPatch(typeof(ShipOrder), nameof(ShipOrder.SetShipSkirmishOrder), new[] { typeof(MissionShip) })]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class SkirmishOrderWithoutTargetPatch
{
    // Skips the order for a missing target in a co-op battle.
    [HarmonyPrefix]
    internal static bool Prefix(MissionShip shipToSkirmish) => !CoopNavalMissionScope.IsActive || shipToSkirmish != null;
}

/// <summary>
/// An engage order at a formation without a hull (the player charging L6's swimmers) would dereference the missing target in
/// UpdateEngageOrder; in a co-op naval battle the order is not given.
/// </summary>
[HarmonyPatch(typeof(ShipOrder), nameof(ShipOrder.SetShipEngageOrder), new[] { typeof(MissionShip) })]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class EngageOrderWithoutTargetPatch
{
    // Skips the order for a missing target in a co-op battle.
    [HarmonyPrefix]
    internal static bool Prefix(MissionShip shipToEngage) => !CoopNavalMissionScope.IsActive || shipToEngage != null;
}

/// <summary>
/// A follow order at a missing hull (follow-me while the main agent swims, so its formation ship is null) would dereference it
/// in SetMovementTargetShip; in a co-op naval battle the order is not given.
/// </summary>
[HarmonyPatch(typeof(ShipOrder), nameof(ShipOrder.SetShipFollowOrder))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class FollowOrderWithoutTargetPatch
{
    // Skips the order for a missing target in a co-op battle.
    [HarmonyPrefix]
    internal static bool Prefix(MissionShip shipToFollow) => !CoopNavalMissionScope.IsActive || shipToFollow != null;
}

/// <summary>
/// Any player formation with live units is selectable, and a player whose hull was captured keeps one with no hull; the naval
/// order setters then call ShipOrder on its null hull. In a co-op naval battle they run on the selection's formations with a
/// hull only, and the full selection is restored after; their callers raise OnOrderIssued after that restore, on the full
/// selection (SetOrderWithPosition raises it itself, on the hulled formations it moved). With no hulled formation left the
/// setter is skipped, as several read SelectedFormations[0].
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ShiplessSelectedFormationOrdersPatch
{
    // The order setters that read every selected formation's hull unchecked.
    internal static IEnumerable<MethodBase> TargetMethods() =>
        new[] { "SetNavalEngageWithTargetFormation", "SetNavalSkirmishWithTargetFormation", "SetNavalStop", "SetNavalRetreat",
            "SetNavalFollowOrder", "SetOrderWithPosition" }
            .Select(name => AccessTools.DeclaredMethod(typeof(NavalOrderController), name));

    // Takes the selected formations without a hull out of the selection for the order, keeping where each stood; skips the
    // setter when that left nothing (the finalizer still restores).
    [HarmonyPrefix]
    internal static bool Prefix(OrderController __instance, out List<(int Index, Formation Formation)> __state)
    {
        __state = null;
        var shipsLogic = NavalShiplessFormations.ShipsLogic;
        if (!CoopNavalMissionScope.IsActive || shipsLogic == null) return true;

        __state = TakeShipless(__instance._selectedFormations, formation =>
            NavalShiplessFormations.ShipOf(shipsLogic, formation, out bool known) != null || !known);
        return RunsOrder(__instance._selectedFormations.Count, __state.Count);
    }

    // Puts them back where they stood, whether the order returned or threw.
    [HarmonyFinalizer]
    internal static void Finalizer(OrderController __instance, List<(int Index, Formation Formation)> __state) =>
        Restore(__instance._selectedFormations, __state);

    /// <summary>Removes the formations <paramref name="hasShip"/> rejects, returning each with its index, in order.</summary>
    internal static List<(int Index, Formation Formation)> TakeShipless(IList<Formation> selected, Func<Formation, bool> hasShip)
    {
        var taken = new List<(int, Formation)>();
        for (int index = 0; index < selected.Count; index++)
        {
            if (!hasShip(selected[index])) taken.Add((index, selected[index]));
        }

        for (int i = taken.Count - 1; i >= 0; i--) selected.RemoveAt(taken[i].Item1);
        return taken;
    }

    /// <summary>Whether the setter runs: not when taking the hull-less formations out emptied the selection.</summary>
    internal static bool RunsOrder(int selectedLeft, int taken) => selectedLeft > 0 || taken == 0;

    /// <summary>Inserts the taken formations back at their indices, lowest first.</summary>
    internal static void Restore(IList<Formation> selected, List<(int Index, Formation Formation)> taken)
    {
        if (taken == null) return;

        foreach (var (index, formation) in taken) selected.Insert(Math.Min(index, selected.Count), formation);
    }
}

/// <summary>
/// A cancelled ramming hands its ignored target back to its hull's AI controller, which a capture or a player at the helm can
/// have replaced (a null or player controller, which the vanilla cast and call fail on); in a co-op battle that call is skipped.
/// </summary>
[HarmonyPatch(typeof(BehaviorNavalRamming), nameof(BehaviorNavalRamming.OnBehaviorCanceled))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class RammingCancelWithoutAiControllerPatch
{
    // Stops the ramming as vanilla does, skipping only the controller call; the ignored target stays recorded, as in vanilla,
    // so CalculateAndSetShipOrders still takes it off the list if the same AI controller comes back.
    [HarmonyPrefix]
    internal static bool Prefix(BehaviorNavalRamming __instance)
    {
        if (!CoopNavalMissionScope.IsActive || __instance._ignoredShip == null) return true;
        if (__instance._formationMainShip?.Controller is AIShipController) return true;

        __instance._isRammingActive = false;
        return false;
    }
}

/// <summary>Reads, for the shipless-formation patches, whether a formation or its closest enemy formation holds a hull.</summary>
internal static class NavalShiplessFormations
{
    /// <summary>The mission's naval ships logic, or null outside a naval mission.</summary>
    public static NavalShipsLogic ShipsLogic => Mission.Current?.GetMissionBehavior<NavalShipsLogic>();

    /// <summary>The formation's hull as vanilla finds it; <paramref name="known"/> is false without the ships logic or a team.</summary>
    public static MissionShip ShipOf(NavalShipsLogic shipsLogic, Formation formation, out bool known)
    {
        known = shipsLogic != null && formation?.Team != null;
        if (!known) return null;

        shipsLogic.GetShip(formation.Team.TeamSide, formation.FormationIndex, out var ship);
        return ship;
    }

    /// <summary>Whether the closest enemy formation holds a hull; true when there is none or it can't be told.</summary>
    public static bool ClosestEnemyHasShip(NavalShipsLogic shipsLogic, Formation formation)
    {
        var enemy = formation?.CachedClosestEnemyFormation?.Formation;
        if (shipsLogic == null || enemy?.Team == null) return true;

        return shipsLogic.GetShipAssignment(enemy.Team.TeamSide, enemy.FormationIndex)?.MissionShip != null;
    }
}
