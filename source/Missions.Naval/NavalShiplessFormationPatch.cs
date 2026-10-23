using Common.Logging;
using HarmonyLib;
using NavalDLC.Missions;
using NavalDLC.Missions.AI.Behaviors;
using NavalDLC.Missions.AI.TeamAI;
using NavalDLC.Missions.MissionLogics;
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

    // Answers 0 for a formation without a hull in a co-op battle instead of dereferencing the missing hull.
    [HarmonyPrefix]
    internal static bool Prefix(BehaviorComponent __instance, ref float __result)
    {
        if (!CoopNavalMissionScope.IsActive || HasShip(__instance.Formation)) return true;

        __result = 0f;
        return false;
    }

    // Whether vanilla finds a hull for the formation; without the ships logic the vanilla weight runs.
    private static bool HasShip(Formation formation)
    {
        var shipsLogic = Mission.Current?.GetMissionBehavior<NavalShipsLogic>();
        if (shipsLogic == null || formation?.Team == null) return true;

        return shipsLogic.GetShip(formation.Team.TeamSide, formation.FormationIndex, out _);
    }
}
