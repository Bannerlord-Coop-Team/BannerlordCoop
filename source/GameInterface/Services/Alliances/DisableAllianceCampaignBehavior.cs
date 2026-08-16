using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Alliances;

[HarmonyPatch(typeof(AllianceCampaignBehavior))]
internal class DisableAllianceCampaignBehavior
{
    [HarmonyPatch(nameof(AllianceCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}
