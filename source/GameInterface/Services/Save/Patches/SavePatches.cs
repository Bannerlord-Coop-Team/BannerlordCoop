using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using TaleWorlds.Core;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(Game), "Save")]
class SavePatches
{
    static bool Prefix(Game __instance, ref string saveName)
    {
        // Disable saving for the client so we don't have to worry about pausing to save
        if (ModInformation.IsClient) return false;

        MessageBroker.Instance.Publish(__instance, new GameSaved(saveName));
        return true;
    }
}
