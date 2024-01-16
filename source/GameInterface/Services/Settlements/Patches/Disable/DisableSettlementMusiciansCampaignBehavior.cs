using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Settlements.Patches.Disable;


[HarmonyPatch(typeof(SettlementMusiciansCampaignBehavior))]
internal class DisableSettlementMusiciansCampaignBehavior
{
    [HarmonyPatch(nameof(SettlementMusiciansCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
