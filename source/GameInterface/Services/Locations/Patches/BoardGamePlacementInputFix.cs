using Common.Logging;
using HarmonyLib;
using SandBox.BoardGames;
using Serilog;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.InputSystem;

namespace GameInterface.Services.Locations.Patches;

/// <summary>
/// Fixes tavern board-game piece placement in co-op. In a co-op mission the game-key RELEASE edge is
/// dropped: <c>IInputContext.IsHotKeyDown("BoardGamePawnDeselect")</c> tracks correctly but
/// <c>IsHotKeyReleased(...)</c> never latches, while the raw mouse-up (<c>Input.IsKeyReleased</c>) does.
/// Vanilla <c>BoardGameBase.HandlePlayerInput</c> gates the "drop" entirely on that missing release edge,
/// so a picked-up pawn can never be placed. This transpiler rewrites the single
/// <c>IsHotKeyReleased("BoardGamePawnDeselect")</c> check to also accept the raw left-mouse release, so
/// vanilla's own drop branch runs unchanged.
/// </summary>
[HarmonyPatch(typeof(BoardGameBase), "HandlePlayerInput")]
internal class BoardGamePlacementInputFix
{
    private static readonly ILogger Logger = LogManager.GetLogger<BoardGamePlacementInputFix>();

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var helper = AccessTools.Method(typeof(BoardGamePlacementInputFix), nameof(HotKeyOrRawMouseReleased));

        bool patched = false;
        for (int i = 1; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Callvirt
                && codes[i].operand is MethodInfo mi && mi.Name == "IsHotKeyReleased"
                && codes[i - 1].opcode == OpCodes.Ldstr && (string)codes[i - 1].operand == "BoardGamePawnDeselect")
            {
                codes[i] = new CodeInstruction(OpCodes.Call, helper);
                patched = true;
                break;
            }
        }

        if (!patched)
            Logger.Error("[BoardGame] Placement input fix could not find the IsHotKeyReleased(\"BoardGamePawnDeselect\") call — game update may have changed the IL.");

        return codes;
    }

    // Same stack as the replaced instance call: (this input context, game-key id). Preserves the original
    // behavior and adds the raw mouse-up as a fallback for the co-op case where the game-key edge is lost.
    private static bool HotKeyOrRawMouseReleased(IInputContext input, string gameKey)
    {
        return input.IsHotKeyReleased(gameKey) || Input.IsKeyReleased(InputKey.LeftMouseButton);
    }
}
