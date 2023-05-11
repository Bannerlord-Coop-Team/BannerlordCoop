using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using TaleWorlds.Core;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(Game), "Save")]
class SavePatches
{
    static void Prefix(Game __instance, ref string saveName)
    {
        MessageBroker.Instance.Publish(__instance, new GameSaved(saveName));
    }
}
