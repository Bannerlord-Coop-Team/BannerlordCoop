using Common;
using Common.Messaging;
using GameInterface.Services.CampaignService.Messages;
using HarmonyLib;
using SandBox.View.Map;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions;

namespace GameInterface.Services.CampaignService.Patches;

/// <summary>
/// Send a message to update campaign options for clients when host closes options menu
/// </summary>
[HarmonyPatch(typeof(MapScreen))]
internal class UpdateCampaignOptionsPatch
{
    [HarmonyPatch(nameof(MapScreen.CloseCampaignOptions))]
    [HarmonyPostfix]
    public static void CloseCampaignOptionsPostfix(MapScreen __instance)
    {
        if (ModInformation.IsClient) return;

        var message = new UpdateCampaignOptions();
        MessageBroker.Instance.Publish(__instance, message);
    }
}

[HarmonyPatch(typeof(OptionsVM))]
internal class ApplyChangedOptionsPatch
{
    [HarmonyPatch(nameof(OptionsVM.ApplyChangedOptions))]
    [HarmonyPostfix]
    public static void ApplyChangedOptionsPostfix()
    {
        if (ModInformation.IsClient) return;

        var message = new UpdateOtherOptions();
        MessageBroker.Instance.Publish(null, message);
    }
}