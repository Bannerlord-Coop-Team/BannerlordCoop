using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Inventory.TradeSkills.Patches;

/// <summary>
/// Handled separately via an interface. This behavior normally assumes only one player
/// </summary>
[HarmonyPatch(typeof(TradeSkillCampaignBehavior))]
internal class DisableTradeSkillCampaignBehavior
{
    [HarmonyPatch(nameof(TradeSkillCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}