using Common;
using GameInterface.Policies;
using GameInterface.Services.Villages.Data;
using GameInterface.Services.Villages.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Villages.Patches;

/// <summary>
/// Each "continue" consequence starts the encounter, so each sends the request to the server instead.
/// </summary>
[HarmonyPatch(typeof(VillageHostileActionCampaignBehavior))]
internal class VillageHostileActionStartPatch
{
    [HarmonyPatch(nameof(VillageHostileActionCampaignBehavior.game_menu_raid_continue_on_consequence))]
    [HarmonyPrefix]
    private static bool RaidPrefix() => RequestOnServer(VillageHostileAction.Raid);

    [HarmonyPatch(nameof(VillageHostileActionCampaignBehavior.game_menu_force_volunteers_resist_continue_on_consequence))]
    [HarmonyPrefix]
    private static bool ForceVolunteersPrefix() => RequestOnServer(VillageHostileAction.ForceVolunteers);

    [HarmonyPatch(nameof(VillageHostileActionCampaignBehavior.game_menu_force_supplies_resist_continue_on_consequence))]
    [HarmonyPrefix]
    private static bool ForceSuppliesPrefix() => RequestOnServer(VillageHostileAction.ForceSupplies);

    private static bool RequestOnServer(VillageHostileAction action)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;

        if (!ContainerProvider.TryResolve<IVillageHostileActionInterface>(out var hostileActionInterface))
            return false;

        hostileActionInterface.RequestHostileAction(action);
        return false;
    }
}

/// <summary>
/// A village with no defenders would hand over supplies or recruits locally from the menu. Every village
/// resists instead, so force actions go through the server-side encounter like any other.
/// </summary>
[HarmonyPatch(typeof(VillageHostileActionCampaignBehavior), nameof(VillageHostileActionCampaignBehavior.WillVillageResistHostileAction))]
internal class VillageAlwaysResistsHostileActionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref bool __result)
    {
        __result = true;
        return false;
    }
}
