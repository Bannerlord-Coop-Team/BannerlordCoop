using Common;
using GameInterface.Policies;
using GameInterface.Services.Villages.Data;
using GameInterface.Services.Villages.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;

namespace GameInterface.Services.Villages.Patches;

/// <summary>
/// v1.5 split the old StartHostileAction(HostileActionType) into one "continue" consequence per
/// action. Each of them starts the encounter, so each sends the request to the server instead.
/// </summary>
[HarmonyPatch(typeof(VillageHostileActionCampaignBehavior))]
internal class VillageHostileActionStartPatch
{
    [HarmonyPatch("game_menu_raid_continue_on_consequence")]
    [HarmonyPrefix]
    private static bool RaidPrefix(MenuCallbackArgs args) => RequestOnServer(VillageHostileAction.Raid);

    [HarmonyPatch("game_menu_force_volunteers_resist_continue_on_consequence")]
    [HarmonyPrefix]
    private static bool ForceVolunteersPrefix(MenuCallbackArgs args) => RequestOnServer(VillageHostileAction.ForceVolunteers);

    [HarmonyPatch("game_menu_force_supplies_resist_continue_on_consequence")]
    [HarmonyPrefix]
    private static bool ForceSuppliesPrefix(MenuCallbackArgs args) => RequestOnServer(VillageHostileAction.ForceSupplies);

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
/// v1.5 lets a village with no defenders give up supplies or recruits without a battle, applied
/// locally by the menu. Co-op authorizes force actions through the server-side encounter, so every
/// village resists and the request goes through <see cref="VillageHostileActionStartPatch"/>.
/// </summary>
[HarmonyPatch(typeof(VillageHostileActionCampaignBehavior), "WillVillageResistHostileAction")]
internal class VillageAlwaysResistsHostileActionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref bool __result)
    {
        __result = true;
        return false;
    }
}
