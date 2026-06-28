using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;

namespace GameInterface.Services.Armies.Patches.Disable;

[HarmonyPatch(typeof(Army))]
internal class DisableArmyHourlyTick
{
    [HarmonyPatch(nameof(Army.HourlyTick))]
    static bool Prefix() => ModInformation.IsServer;
}
