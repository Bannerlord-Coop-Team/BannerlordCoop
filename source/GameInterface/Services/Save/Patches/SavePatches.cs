using Common.Messaging;
using GameInterface.Services.Save.Messages;
using HarmonyLib;
using TaleWorlds.Core;

namespace Coop.Mod.Patch.World
{
    [HarmonyPatch(typeof(Game), "Save")]
    class SavePatches
    {
        static void Prefix(Game __instance, ref string saveName)
        {
            MessageBroker.Instance.Publish(__instance, new GameSaved(saveName));
        }
    }
}
