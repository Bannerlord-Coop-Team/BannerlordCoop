using Common;
using GameInterface.Policies;
using GameInterface.Services.Villages.Data;
using GameInterface.Services.Villages.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Villages.Patches;

/// <summary>
/// v1.5 split the old StartHostileAction(HostileActionType) into one "continue" consequence per
/// action and per answer of the village. Each of them changes the world, so each sends the request
/// to the server instead. The server decides again whether the village resists (#4587).
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

    [HarmonyPatch(nameof(VillageHostileActionCampaignBehavior.game_menu_force_volunteers_no_resist_continue_on_consequence))]
    [HarmonyPrefix]
    private static bool ForceVolunteersNoResistPrefix() => RequestOnServer(VillageHostileAction.ForceVolunteers);

    [HarmonyPatch(nameof(VillageHostileActionCampaignBehavior.game_menu_force_supplies_no_resist_continue_on_consequence))]
    [HarmonyPrefix]
    private static bool ForceSuppliesNoResistPrefix() => RequestOnServer(VillageHostileAction.ForceSupplies);

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
