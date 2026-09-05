using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch]
internal static class ClanPartyRolePermissionsPatches
{
    [HarmonyPatch(typeof(ClanRoleItemVM), nameof(ClanRoleItemVM.ExecuteToggleRoleSelection))]
    [HarmonyPrefix]
    public static bool ToggleRoleSelectionPrefix(ClanRoleItemVM __instance)
    {
        return __instance.IsEnabled && SharedClanPermissions.CanAssignRoles(__instance._party);
    }

    [HarmonyPatch(typeof(ClanRoleMemberItemVM), nameof(ClanRoleMemberItemVM.ExecuteAssignHeroToRole))]
    [HarmonyPrefix]
    public static bool AssignHeroToRolePrefix(ClanRoleMemberItemVM __instance)
    {
        return SharedClanPermissions.CanAssignRoles(__instance._party);
    }
}
