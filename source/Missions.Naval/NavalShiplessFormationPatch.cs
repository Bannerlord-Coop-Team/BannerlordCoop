using Common.Logging;
using HarmonyLib;
using NavalDLC.Missions;
using NavalDLC.Missions.AI.Behaviors;
using NavalDLC.Missions.AI.TeamAI;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
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
/// The naval behaviours that read their formation's hull in GetAiWeight weigh nothing while that formation has no hull, the
/// state a capture, a sinking hull's departure or a removal can leave a formation with units in, in a co-op naval battle.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ShiplessFormationBehaviorWeightPatch
{
    // The behaviours whose weight dereferences the formation's hull (_formationMainShip) unchecked.
    internal static IEnumerable<MethodBase> TargetMethods() =>
        new[] { typeof(BehaviorNavalRemoveConnection), typeof(BehaviorNavalRamming), typeof(BehaviorNavalSkirmish) }
            .Select(type => AccessTools.DeclaredMethod(type, "GetAiWeight"));

    // Answers 0 for a formation without a hull, or a behaviour with no cached hull, in a co-op battle; a behaviour whose
    // cached hull is null while its formation has one again (a hull joined it) re-reads it, as vanilla's own refresh does.
    [HarmonyPrefix]
    internal static bool Prefix(BehaviorComponent __instance, ref float __result)
    {
        if (!CoopNavalMissionScope.IsActive) return true;

        var cached = MainShipOf(__instance);
        var ship = ShipOf(__instance.Formation, out bool known);
        if (known && ship == null) return Weightless(ref __result);
        if (cached != null) return true;
        return known ? SetMainShip(__instance, ship) : Weightless(ref __result);
    }

    // Answers 0 in place of the vanilla weight.
    private static bool Weightless(ref float result)
    {
        result = 0f;
        return false;
    }

    // The formation's hull as vanilla finds it; unknown without the ships logic or a team.
    private static MissionShip ShipOf(Formation formation, out bool known)
    {
        var shipsLogic = Mission.Current?.GetMissionBehavior<NavalShipsLogic>();
        known = shipsLogic != null && formation?.Team != null;
        if (!known) return null;

        shipsLogic.GetShip(formation.Team.TeamSide, formation.FormationIndex, out var ship);
        return ship;
    }

    /// <summary>The hull a patched behaviour cached for its formation, which vanilla's refresh leaves null after a capture.</summary>
    internal static MissionShip MainShipOf(BehaviorComponent behavior) => behavior switch
    {
        BehaviorNavalRemoveConnection removeConnection => removeConnection._formationMainShip,
        BehaviorNavalRamming ramming => ramming._formationMainShip,
        BehaviorNavalSkirmish skirmish => skirmish._formationMainShip,
        _ => null,
    };

    // Re-reads the formation's hull into a patched behaviour, then lets the vanilla weight run on it.
    private static bool SetMainShip(BehaviorComponent behavior, MissionShip ship)
    {
        if (behavior is BehaviorNavalRemoveConnection removeConnection) removeConnection._formationMainShip = ship;
        else if (behavior is BehaviorNavalRamming ramming) ramming._formationMainShip = ship;
        else if (behavior is BehaviorNavalSkirmish skirmish) skirmish._formationMainShip = ship;
        return true;
    }
}
