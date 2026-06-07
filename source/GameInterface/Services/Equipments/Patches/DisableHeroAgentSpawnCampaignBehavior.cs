using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Heroes.Patches;

/// <summary>
/// This behavior does nothing
/// </summary>
[HarmonyPatch(typeof(NPCEquipmentsCampaignBehavior))]
internal class DisableNPCEquipmentsCampaignBehavior
{
    [HarmonyPatch(nameof(NPCEquipmentsCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
