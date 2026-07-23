using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Characters.Patches;

[HarmonyPatch(typeof(TradeSkillCampaignBehavior))]
internal class DisableTradeSkillCampaingBehavior
{
    [HarmonyPatch(nameof(TradeSkillCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
