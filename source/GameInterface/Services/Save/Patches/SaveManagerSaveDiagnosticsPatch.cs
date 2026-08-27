using Common.Logging;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Save;

namespace GameInterface.Services.Save.Patches;

[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Save))]
internal static class SaveManagerSaveDiagnosticsPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(SaveManagerSaveDiagnosticsPatch));
    private const string NoFailure = "saveResult=none|saveErrors=<no details>";
    private static string lastFailure = NoFailure;

    internal static string LastFailure => Volatile.Read(ref lastFailure);

    [HarmonyPostfix]
    internal static void Postfix(SaveOutput __result)
    {
        if (__result == null)
        {
            Volatile.Write(ref lastFailure, FormatFailure("<null>", null));
            Logger.Error("Game save failed without a save result.");
            return;
        }

        if (__result.Successful)
        {
            Volatile.Write(ref lastFailure, NoFailure);
            return;
        }

        string failure = FormatFailure(
            __result.Result.ToString(),
            __result.Errors?.Select(error => error?.Message));
        Volatile.Write(ref lastFailure, failure);

        Logger.Error("Game save failed: {SaveFailure}", failure);
    }

    internal static string FormatFailure(string saveResult, IEnumerable<string> errors) =>
        $"saveResult={saveResult}|saveErrors={FormatErrorMessages(errors)}";

    internal static string FormatErrorMessages(IEnumerable<string> errors)
    {
        if (errors == null) return "<no details>";

        string[] messages = errors
            .Select((message, index) => $"[{index}] {message ?? "<empty>"}")
            .ToArray();
        return messages.Length == 0 ? "<no details>" : string.Join(" | ", messages);
    }
}
