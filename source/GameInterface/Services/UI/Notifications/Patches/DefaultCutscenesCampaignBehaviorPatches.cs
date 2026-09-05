using Common;
using Common.Messaging;
using GameInterface.Services.UI.Cutscenes.Messages;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;

namespace GameInterface.Services.UI.Notifications.Patches;

[HarmonyPatch]
internal static class DefaultCutscenesCampaignBehaviorPatches
{
    [HarmonyPatch(typeof(DefaultCutscenesCampaignBehavior), nameof(DefaultCutscenesCampaignBehavior.RegisterEvents))]
    [HarmonyPrefix]
    public static bool RegisterCutScenesPrefix()
    {
        return ModInformation.IsClient; // Disable cutscenes on server as they are not needed
    }

    [HarmonyPatch(typeof(MBInformationManager), nameof(MBInformationManager.ShowSceneNotification))]
    [HarmonyPostfix]
    public static void AfterShowSceneNotification(SceneNotificationData data)
    {
        MessageBroker.Instance.Publish(null, new SceneNotificationQueued(data));
    }

    [HarmonyPatch(typeof(SceneNotificationVM), nameof(SceneNotificationVM.ClearData))]
    [HarmonyPrefix]
    public static void BeforeClearData(SceneNotificationVM __instance, out SceneNotificationData __state)
    {
        __state = __instance.ActiveData;
    }

    [HarmonyPatch(typeof(SceneNotificationVM), nameof(SceneNotificationVM.ClearData))]
    [HarmonyPostfix]
    public static void AfterClearData(SceneNotificationData __state)
    {
        if (__state == null) return;

        MessageBroker.Instance.Publish(null, new SceneNotificationClosed(__state));
    }
}
