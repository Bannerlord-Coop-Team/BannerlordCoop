using Common;
using GameInterface.Services.Villages.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace GameInterface.Services.Villages.Patches;

internal static class VillageHostileActionConditionPatch
{
    private static readonly TextObject CooldownTooltip = new("{=!}The village is recovering from a recent hostile action.");

    public static void ApplySyncedCooldown(MenuCallbackArgs args, bool result)
    {
        if (ModInformation.IsServer) return;
        if (!result || !args.IsEnabled) return;

        var settlement = Settlement.CurrentSettlement;
        if (settlement == null)
            return;

        if (!ContainerProvider.TryResolve<IVillageHostileActionInterface>(out var hostileActionInterface))
            return;

        if (!hostileActionInterface.TryGetForceActionCooldown(settlement, out _))
            return;

        args.IsEnabled = false;
        args.Tooltip = CooldownTooltip;
    }
}

[HarmonyPatch(typeof(VillageHostileActionCampaignBehavior), "game_menu_village_hostile_action_force_volunteers_condition")]
internal class VillageForceVolunteersConditionPatch
{
    [HarmonyPostfix]
    private static void Postfix(MenuCallbackArgs args, bool __result)
    {
        VillageHostileActionConditionPatch.ApplySyncedCooldown(args, __result);
    }
}

[HarmonyPatch(typeof(VillageHostileActionCampaignBehavior), "game_menu_village_hostile_action_take_food_on_condition")]
internal class VillageForceSuppliesConditionPatch
{
    [HarmonyPostfix]
    private static void Postfix(MenuCallbackArgs args, bool __result)
    {
        VillageHostileActionConditionPatch.ApplySyncedCooldown(args, __result);
    }
}