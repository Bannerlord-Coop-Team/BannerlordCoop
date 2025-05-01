using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.Core;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(Game), "Save")]
class SavePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<SavePatches>();

    static bool Prefix(Game __instance, ref string saveName)
    {
        // Disable saving for the client so we don't have to worry about pausing to save
        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
        messageBroker?.Publish(__instance, new GameSaved(saveName));
        return true;
    }
}
