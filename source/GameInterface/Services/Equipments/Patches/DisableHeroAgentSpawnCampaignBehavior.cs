using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches;

/// <summary>
/// This behavior does nothing
/// </summary>
[HarmonyPatch(typeof(NPCEquipmentsCampaignBehavior))]
internal class DisableNPCEquipmentsCampaignBehavior
{
    [HarmonyPatch(nameof(NPCEquipmentsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
