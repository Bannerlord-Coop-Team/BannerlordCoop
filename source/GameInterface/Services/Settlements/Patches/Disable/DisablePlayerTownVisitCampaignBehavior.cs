using Common;
using Common.Messaging;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace GameInterface.Services.Settlements.Patches.Disable;

[HarmonyPatch(typeof(PlayerTownVisitCampaignBehavior))]
internal class DisablePlayerTownVisitCampaignBehavior
{
    /// <summary>
    /// Disables entering the jail from the castle menu
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch("game_menu_castle_dungeon_on_consequence")]
    private static bool DisableCastleDungeon()
    {
        return false;
    }

    [HarmonyPatch(nameof(PlayerTownVisitCampaignBehavior.game_menu_manage_garrison_on_consequence))]
    [HarmonyPrefix]
    private static bool ManageGarrisonPrefix(PlayerTownVisitCampaignBehavior __instance)
    {
        return HandleGarrison(__instance, DonateType.ManageTroops);
    }

    [HarmonyPatch(nameof(PlayerTownVisitCampaignBehavior.game_menu_leave_troops_garrison_on_consequece))]
    [HarmonyPrefix]
    private static bool DonateGarrisonPrefix(PlayerTownVisitCampaignBehavior __instance)
    {
        return HandleGarrison(__instance, DonateType.DonateTroops);
    }

    [HarmonyPatch(nameof(PlayerTownVisitCampaignBehavior.game_menu_castle_leave_prisoners_on_consequence))]
    [HarmonyPrefix]
    private static bool LeavePrisonersPrefix(PlayerTownVisitCampaignBehavior __instance)
    {
        return HandleGarrison(__instance, DonateType.DonatePrisoners);
    }

    private static bool HandleGarrison(PlayerTownVisitCampaignBehavior instance, DonateType donateType)
    {
        Settlement settlement = Hero.MainHero.CurrentSettlement;

        if (settlement == null)
            return false;

        if (settlement.Town.GarrisonParty != null)
        {
            OpenGarrisonScreen(settlement, donateType);
            return false;
        }

        MessageBroker.Instance.Publish(instance, new NewGarrisonParty(settlement));
        CheckGarrisonReady(settlement, donateType);
        return false;
    }

    private static void OpenGarrisonScreen(Settlement settlement, DonateType donateType)
    {
        switch (donateType)
        {
            case DonateType.DonateTroops:
                PartyScreenHelper.OpenScreenAsDonateGarrisonWithCurrentSettlement();
                break;

            case DonateType.DonatePrisoners:
                PartyScreenHelper.OpenScreenAsDonatePrisoners();
                break;

            case DonateType.ManageTroops:
                PartyScreenHelper.OpenScreenAsManageTroops(settlement.Town.GarrisonParty);
                break;
        }
    }

    private static void CheckGarrisonReady(Settlement settlement, DonateType donateType)
    {
        if (settlement.Town.GarrisonParty != null)
        {
            OpenGarrisonScreen(settlement, donateType);
            return;
        }
        GameThread.EnqueueSafe(() => CheckGarrisonReady(settlement, donateType));
    }

    [HarmonyPatch(nameof(PlayerTownVisitCampaignBehavior.game_menu_town_on_init))]
    [HarmonyPrefix]
    private static bool GameMenuTownOnInitPrefix(MenuCallbackArgs args)
    {
        PlayerTownVisitCampaignBehavior.SetIntroductionText(Settlement.CurrentSettlement, false);
        PlayerTownVisitCampaignBehavior.UpdateMenuLocations(args.MenuContext.GameMenu.StringId);
        if (MenuHelper.CheckAndOpenNextLocation(args))
        {
            return false;
        }
        args.MenuTitle = new TextObject("{=mVKcvY2U}Town Center", null);
        return false;
    }

    [HarmonyPatch(nameof(PlayerTownVisitCampaignBehavior.game_menu_castle_on_init))]
    [HarmonyPrefix]
    private static bool GameMenuCastleOnInitPrefix(MenuCallbackArgs args)
    {
        PlayerTownVisitCampaignBehavior.SetIntroductionText(Settlement.CurrentSettlement, true);
        PlayerTownVisitCampaignBehavior.UpdateMenuLocations(args.MenuContext.GameMenu.StringId);
        if (Campaign.Current.GameMenuManager.NextLocation != null)
        {
            PlayerEncounter.LocationEncounter.CreateAndOpenMissionController(Campaign.Current.GameMenuManager.NextLocation, Campaign.Current.GameMenuManager.PreviousLocation, null, null);
            Campaign.Current.GameMenuManager.NextLocation = null;
            Campaign.Current.GameMenuManager.PreviousLocation = null;
        }
        args.MenuTitle = new TextObject("{=sVXa3zFx}Castle", null);
        return false;
    }
}

internal enum DonateType
{
    ManageTroops,
    DonateTroops,
    DonatePrisoners
}