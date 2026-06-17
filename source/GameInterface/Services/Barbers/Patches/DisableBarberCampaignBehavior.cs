using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.Banners.Patches;

[HarmonyPatch(typeof(BarberCampaignBehavior))]
internal class DisableBarberCampaignBehavior
{
    [HarmonyPatch(nameof(BarberCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
