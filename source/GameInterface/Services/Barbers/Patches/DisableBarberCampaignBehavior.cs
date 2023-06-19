using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Banners.Patches;

[HarmonyPatch(typeof(BarberCampaignBehavior))]
internal class DisableBarberCampaignBehavior
{
    [HarmonyPatch(nameof(BarberCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
