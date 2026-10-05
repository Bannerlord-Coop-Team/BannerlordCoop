using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch]
internal static class ClanPartyRolePermissionsPatches
{
    [HarmonyPatch(typeof(ClanRoleItemVM), nameof(ClanRoleItemVM.ExecuteToggleRoleSelection))]
    [HarmonyPrefix]
    public static bool ToggleRoleSelectionPrefix(ClanRoleItemVM __instance)
    {
        return __instance.IsEnabled && CoopClanPermissions.CanAssignRoles(__instance._party);
    }

    /// <summary>
    /// v1.5 removed ClanRoleMemberItemVM; every role assignment from the clan screen now goes
    /// through <see cref="ClanPartiesVM.AssignHeroToRole"/> (a popup pick, or its confirmation).
    /// </summary>
    [HarmonyPatch(typeof(ClanPartiesVM), nameof(ClanPartiesVM.AssignHeroToRole))]
    [HarmonyPrefix]
    public static bool AssignHeroToRolePrefix(ClanPartiesVM __instance)
    {
        return CoopClanPermissions.CanAssignRoles(__instance.CurrentSelectedParty?.Party?.MobileParty);
    }
}
