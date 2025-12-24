using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Barbers.Patches;

[HarmonyPatch(typeof(BarberCampaignBehavior))]
internal class DisableBarberCampaignBehavior
{
    [HarmonyPatch(nameof(BarberCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
