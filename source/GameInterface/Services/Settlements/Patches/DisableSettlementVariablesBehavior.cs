using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Settlements.Patches;


[HarmonyPatch(typeof(SettlementVariablesBehavior))]
internal class DisableSettlementVariablesBehavior
{
    [HarmonyPatch(nameof(SettlementVariablesBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
