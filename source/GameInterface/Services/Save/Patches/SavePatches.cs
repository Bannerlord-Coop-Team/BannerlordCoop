using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using TaleWorlds.Core;
using GameInterface.Policies;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(Game), "Save")]
class SavePatches
{
    static bool Prefix(Game __instance, ref string saveName)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer)
        {
            MessageBroker.Instance.Publish(__instance, new GameSaved(saveName));
        }

        return true;
    }
}
