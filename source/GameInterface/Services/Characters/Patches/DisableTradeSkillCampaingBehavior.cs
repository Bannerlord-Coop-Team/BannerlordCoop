using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Characters.Patches;

[HarmonyPatch(typeof(TradeSkillCampaignBehavior))]
internal class DisableTradeSkillCampaingBehavior
{
    [HarmonyPatch(nameof(TradeSkillCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
