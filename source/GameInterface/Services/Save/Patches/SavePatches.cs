using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Save.Commands;
using GameInterface.Services.Save.Messages;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Save.Patches;

[HarmonyPatch(typeof(Game), "Save")]
class SavePatches
{
    internal static bool Prefix(Game __instance, ref string saveName, ISaveDriver driver, Action<SaveResult> onSaveCompleted)
    {
#if DEBUG
        // Disk-save callbacks must see failure, never a fictitious file from an in-memory redirect.
        if (ModInformation.IsNavalLab && driver is not CoopInMemSaveDriver)
        {
            LogManager.GetLogger<SavePatches>().Warning("Naval lab denied disk save {SaveName}", saveName);
            onSaveCompleted?.Invoke(SaveResult.GeneralFailure);
            return false;
        }
#endif
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
