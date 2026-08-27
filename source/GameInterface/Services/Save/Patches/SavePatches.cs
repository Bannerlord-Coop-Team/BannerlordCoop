using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Save.Commands;
using GameInterface.Services.Save.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Save.Patches;

[HarmonyPatch(typeof(Game), "Save")]
class SavePatches
{
    static bool Prefix(Game __instance, ref string saveName, ISaveDriver driver)
    {
        if (ModInformation.IsServer && ShouldPublishGameSaved(driver))
        {
            MessageBroker.Instance.Publish(__instance, new GameSaved(saveName));
        }

        return true;
    }

    internal static bool ShouldPublishGameSaved(ISaveDriver driver)
    {
        // GameSaved writes the co-op session sidecar, so only publish it for a normal campaign save.
        return driver is FileDriver;
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
