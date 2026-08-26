using Common.Logging;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Save;

namespace GameInterface.Services.Save.Patches;

[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Save))]
internal static class SaveManagerSaveDiagnosticsPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(SaveManagerSaveDiagnosticsPatch));

    [HarmonyPostfix]
    internal static void Postfix(SaveOutput __result)
    {
        if (__result == null)
        {
            Logger.Error("Game save failed without a save result.");
            return;
        }

        if (__result.Successful) return;

        Logger.Error(
            "Game save failed with {SaveResult}: {SaveErrors}",
            __result.Result,
            FormatErrorMessages(__result.Errors?.Select(error => error?.Message)));
    }

    internal static string FormatErrorMessages(IEnumerable<string> errors)
    {
        if (errors == null) return "<no details>";

        string[] messages = errors
            .Select((message, index) => $"[{index}] {message ?? "<empty>"}")
            .ToArray();
        return messages.Length == 0 ? "<no details>" : string.Join(" | ", messages);
    }
}
