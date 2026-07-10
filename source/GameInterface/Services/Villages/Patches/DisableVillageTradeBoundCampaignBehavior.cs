using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

[HarmonyPatch(typeof(VillageTradeBoundCampaignBehavior))]
internal class DisableVillageTradeBoundCampaignBehavior
{
    [HarmonyPatch(nameof(VillageTradeBoundCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}