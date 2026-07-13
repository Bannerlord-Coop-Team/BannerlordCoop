using Common;
using Common.Messaging;
using GameInterface.Services.Barbers.Messages;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Barbers.Patches;

[HarmonyPatch(typeof(BarberCampaignBehavior))]
internal class BarberCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(BarberCampaignBehavior.ChargeThePlayer))]
    [HarmonyPrefix]
    public static bool ChargeThePlayerPrefix(BarberCampaignBehavior __instance)
    {
        if (ModInformation.IsServer) return true;

        var message = new BarberChargesPlayer(Hero.MainHero);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }
}
