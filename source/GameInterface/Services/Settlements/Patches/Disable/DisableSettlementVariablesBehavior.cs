using Common;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Settlements.Patches.Disable;


[HarmonyPatch(typeof(SettlementVariablesBehavior))]
internal class DisableSettlementVariablesBehavior
{
    [HarmonyPatch(nameof(SettlementVariablesBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }
}
