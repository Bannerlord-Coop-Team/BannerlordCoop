using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Save.Commands;
using GameInterface.Services.Save.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Save.Patches;

[HarmonyPatch(typeof(Game), "Save")]
class SavePatches
{
    static bool Prefix(Game __instance, ref string saveName)
    {
        if (ModInformation.IsServer)
        {
            MessageBroker.Instance.Publish(__instance, new GameSaved(saveName));
        }

        return true;
    }
}

[HarmonyPatch(typeof(SaveHandler), "OnSaveStarted")]
internal class SaveStartedPatch
{
    static void Prefix(SaveHandler __instance)
    {
        if (ModInformation.IsServer)
        {
            MessageBroker.Instance.Publish(__instance, new GameSaveStateChanged(true));
            SaveDebugCommand.HoldForEvidenceIfRequested();
        }
    }
}

[HarmonyPatch(typeof(SaveHandler), "OnSaveEnded")]
internal class SaveEndedPatch
{
    static void Postfix(SaveHandler __instance)
    {
        if (ModInformation.IsServer)
        {
            MessageBroker.Instance.Publish(__instance, new GameSaveStateChanged(false));
        }
    }
}
