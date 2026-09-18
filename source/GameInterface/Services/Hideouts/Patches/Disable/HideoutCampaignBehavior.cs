using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Hideouts.Handlers;
using GameInterface.Services.Hideouts.Messages;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Hideouts.Patches.Disable;

[HarmonyPatch(typeof(HideoutCampaignBehavior))]
internal class HideoutCampaignBehaviorPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<HideoutCampaignBehaviorPatch>();
    internal const string WaitingMenu = "coop_hideout_waiting";
    internal const string JoinMenu = "coop_hideout_join";

    [HarmonyPatch(nameof(HideoutCampaignBehavior.ArrangeHideoutTroopCountsForMission))]
    [HarmonyPrefix]
    private static bool ArrangeHideoutTroopCountsForMission()
    {
        return ModInformation.IsServer;
    }

    [HarmonyPatch("OnTroopRosterManageDone")]
    [HarmonyPrefix]
    private static bool OnTroopRosterManageDone(HideoutCampaignBehavior __instance,
        TaleWorlds.CampaignSystem.Roster.TroopRoster hideoutTroops, bool isDirectAssault)
    {
        if (ModInformation.IsServer)
            return true;

        var settlement = Settlement.CurrentSettlement;
        if (settlement?.IsHideout != true)
        {
            Logger.Error("Cannot prepare hideout mission because the current settlement is not a hideout");
            return false;
        }

        if (!ContainerProvider.TryResolve<HideoutRaidHandler>(out var coordinator))
        {
            Logger.Error("Unable to resolve hideout mission preparation coordinator");
            return false;
        }

        coordinator.StartRaid(__instance, hideoutTroops, isDirectAssault);
        return false;
    }

    [HarmonyPatch("game_menu_encounter_attack_on_consequence")]
    [HarmonyPrefix]
    private static bool SelectSharedTroops(MenuCallbackArgs args, bool isDirectAssault)
    {
        if (ModInformation.IsServer) return true;
        if (ContainerProvider.TryResolve<HideoutRaidHandler>(out var coordinator))
            coordinator.OpenTroopSelection(args, isDirectAssault);
        return false;
    }

    [HarmonyPatch("OnSessionLaunched")]
    [HarmonyPostfix]
    private static void AddJoinOption(HideoutCampaignBehavior __instance, CampaignGameStarter campaignGameStarter)
    {
        if (ModInformation.IsServer) return;
        campaignGameStarter.AddGameMenu(WaitingMenu,
            "{=coop_hideout_waiting}Waiting for {COOP_HIDEOUT_HERO} to set up their attack.", InitCoopMenu);
        campaignGameStarter.AddGameMenu(JoinMenu,
            "{=coop_hideout_started}{COOP_HIDEOUT_HERO} has started the hideout attack.", InitCoopMenu);
        foreach (var menu in new[] { "hideout_place", "hideout_after_wait", "join_encounter", JoinMenu })
            campaignGameStarter.AddGameMenuOption(menu, "coop_join_hideout", "Join hideout attack",
                CanJoinHideout, JoinHideout);
        foreach (var menu in new[] { WaitingMenu, JoinMenu })
            campaignGameStarter.AddGameMenuOption(menu, "leave", "{=3sRdGQou}Leave", args =>
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                return true;
            }, __instance.game_menu_hideout_leave_on_consequence, isLeave: true);
    }

    private static void InitCoopMenu(MenuCallbackArgs args)
    {
        SetPreparingHero();
        args.MenuContext.SetBackgroundMeshName(Settlement.CurrentSettlement.Hideout.WaitMeshName);
        args.MenuContext.SetPanelSound("event:/ui/panels/settlement_hideout");
    }

    private static void SetPreparingHero()
    {
        var settlement = Settlement.CurrentSettlement;
        if (settlement?.IsHideout != true || !ContainerProvider.TryResolve<IHideoutPreparation>(out var preparation)) return;
        var hero = preparation.GetAttackLeader(settlement);
        MBTextManager.SetTextVariable("COOP_HIDEOUT_HERO", hero?.Name ?? TextObject.GetEmpty());
    }

    internal static void RefreshCoopMenu(MenuContext context)
    {
        if (ModInformation.IsServer || Mission.Current != null || MissionState.Current != null ||
            Game.Current?.GameStateManager?.ActiveState is not MapState ||
            MobileParty.MainParty?.CurrentSettlement?.IsHideout != true) return;
        var encounter = PlayerEncounter.Current;
        if (encounter?._mapEvent?.IsHideoutBattle == true &&
            (encounter._mapEvent.BattleState != BattleState.None ||
             MapEventInitializationBarrier.IsBattleResultEncounter(encounter))) return;
        var current = context.GameMenu?.StringId;
        if (current != "hideout_place" && current != "hideout_after_wait" && current != "hideout_wait" &&
            current != WaitingMenu && current != JoinMenu) return;
        if (!ContainerProvider.TryResolve<IHideoutPreparation>(out var preparation)) return;

        var state = preparation.GetState(MobileParty.MainParty.CurrentSettlement, MobileParty.MainParty);
        var target = state == HideoutEntryState.Waiting ? WaitingMenu :
            state == HideoutEntryState.Join ? JoinMenu :
            current == WaitingMenu || current == JoinMenu ? "hideout_place" : current;
        var previousText = context.GameMenu.GetText().ToString();
        SetPreparingHero();
        if (target != current)
            context.SwitchToMenu(target);
        else if ((current == WaitingMenu || current == JoinMenu) && previousText != context.GameMenu.GetText().ToString())
            context.Refresh();
    }

    internal static bool HideStartOptionsWhileWaiting(ref bool __result)
    {
        if (ModInformation.IsServer || !ContainerProvider.TryResolve<IHideoutPreparation>(out var preparation) ||
            preparation.GetState(Settlement.CurrentSettlement, MobileParty.MainParty) == HideoutEntryState.Start)
            return true;
        __result = false;
        return false;
    }

    private static bool CanJoinHideout(MenuCallbackArgs args)
    {
        var battle = Settlement.CurrentSettlement?.Party.MapEvent ?? PlayerEncounter.EncounteredBattle;
        args.optionLeaveType = GameMenuOption.LeaveType.Mission;
        args.IsEnabled = Hero.MainHero?.IsWounded == false;
        return battle?.EventType == MapEvent.BattleTypes.Hideout && !battle.IsFinalized &&
            battle.BattleState == BattleState.None && (battle.Component as HideoutEventComponent)?.IsSendTroops != true;
    }

    private static void JoinHideout(MenuCallbackArgs args)
    {
        if (ContainerProvider.TryResolve<HideoutRaidHandler>(out var coordinator))
            coordinator.OpenTroopSelection(args, isDirectAssault: true);
    }

    [HarmonyPatch("hideout_send_troops_result_failure_on_init")]
    [HarmonyPrefix]
    private static void HideoutSendTroopsResultFailureOnInit()
    {
        PublishConsequence(HideoutCampaignConsequence.SetAttackCooldown);
    }

    [HarmonyPatch("game_menu_hideout_place_on_init")]
    [HarmonyPrefix]
    private static bool GameMenuHideoutPlaceOnInit()
    {
        var encounter = PlayerEncounter.Current;
        if (encounter == null)
            return true;

        // The server already cleared the hideout; native init would enter it again before showing loot.
        if (ModInformation.IsClient && encounter._mapEvent?.IsHideoutBattle == true &&
            MapEventInitializationBarrier.IsBattleResultEncounter(encounter)) return false;

        var battle = PlayerEncounter.Battle;
        if (battle != null && battle.WinningSide == encounter.PlayerSide)
            PublishConsequence(HideoutCampaignConsequence.GrantClearRewards);
        return true;
    }

    [HarmonyPatch("hideout_send_troops_result_success_consequence")]
    [HarmonyPrefix]
    private static void HideoutSendTroopsResultSuccessConsequence()
    {
        PublishConsequence(HideoutCampaignConsequence.GrantClearRewards);
    }

    [HarmonyPatch(nameof(HideoutCampaignBehavior.HourlyTickSettlement))]
    [HarmonyPrefix]
    public static bool HourlyTickSettlement(Settlement settlement)
    {
        if (!ModInformation.IsServer)
        {
            return false;
        }

        if (settlement.IsHideout && settlement.Hideout.IsInfested && !settlement.Hideout.IsSpotted)
        {
            float hideoutSpottingDistance = Campaign.Current.Models.MapVisibilityModel.GetHideoutSpottingDistance();

            if (ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) == false)
                return false;

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
                return false;

            foreach (var item in playerManager.Players)
            {
                if (objectManager.TryGetObject<MobileParty>(item.MobilePartyId, out var mobileParty) && mobileParty.IsActive)
                {
                    float num = mobileParty.Position.DistanceSquared(settlement.Position);
                    float num2 = 1f - num / (hideoutSpottingDistance * hideoutSpottingDistance);
                    if (num2 > 0f && settlement.Parties.Count > 0 && MBRandom.RandomFloat < num2 && !settlement.Hideout.IsSpotted)
                    {
                        settlement.Hideout.IsSpotted = true;
                        settlement.IsVisible = true;
                        CampaignEventDispatcher.Instance.OnHideoutSpotted(mobileParty.Party, settlement.Party);
                        break;
                    }
                }
            }
        }
        return false;
    }

    private static void PublishConsequence(HideoutCampaignConsequence consequence, Settlement settlement = null)
    {
        if (!ModInformation.IsClient)
            return;

        settlement ??= Settlement.CurrentSettlement;
        if (settlement?.IsHideout != true)
            return;

        MessageBroker.Instance.Publish(
            settlement,
            new HideoutCampaignConsequenceRequested(settlement, consequence));
    }
}

[HarmonyPatch(typeof(MenuContext), nameof(MenuContext.OnTick))]
internal static class HideoutMenuUpdatePatch
{
    private static void Postfix(MenuContext __instance) => HideoutCampaignBehaviorPatch.RefreshCoopMenu(__instance);
}

[HarmonyPatch(typeof(MapState), "OnMapModeTick")]
internal static class HideoutResultUpdatePatch
{
    private static void Postfix(MapState __instance)
    {
        if (ModInformation.IsServer || Mission.Current != null || MissionState.Current != null ||
            Game.Current?.GameStateManager?.ActiveState != __instance) return;
        var encounter = PlayerEncounter.Current;
        if (encounter?._mapEvent?.IsHideoutBattle != true ||
            !MapEventInitializationBarrier.IsBattleResultEncounter(encounter) ||
            Campaign.Current.MapEventManager.MapEvents.Contains(encounter._mapEvent)) return;

        // Custom hideout menus and an empty map have no native menu-init callback to resume the loot flow.
        PlayerEncounter.Update();
    }
}

[HarmonyPatch]
internal static class HideoutStartOptionsPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in new[] { "game_menu_hideout_sneak_in_on_condition", "game_menu_assault_hideout_parties_on_condition",
            "game_menu_send_troops_hideout_on_condition", "game_menu_wait_until_nightfall_on_condition" })
            yield return AccessTools.Method(typeof(HideoutCampaignBehavior), method);
    }

    private static bool Prefix(ref bool __result) => HideoutCampaignBehaviorPatch.HideStartOptionsWhileWaiting(ref __result);
}

[HarmonyPatch]
internal class HideoutJoinEncounterOptionsPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_join_encounter_help_attackers_on_condition");
        yield return AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_join_encounter_help_defenders_on_condition");
    }

    [HarmonyPrefix]
    private static bool Prefix(ref bool __result)
    {
        if (ModInformation.IsServer || PlayerEncounter.EncounteredBattle?.IsHideoutBattle != true)
            return true;

        // Hideouts join through the shared troop selection.
        __result = false;
        return false;
    }
}
