using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Characters.Patches;

[HarmonyPatch(typeof(TradeSkillCampaingBehavior))]
internal class DisableTradeSkillCampaingBehavior
{
    [HarmonyPatch(nameof(TradeSkillCampaingBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
