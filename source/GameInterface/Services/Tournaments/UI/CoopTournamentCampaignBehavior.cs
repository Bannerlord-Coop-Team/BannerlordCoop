using Common;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Tournaments.Data;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Localization;

namespace GameInterface.Services.Tournaments.UI;

public sealed class CoopTournamentCampaignBehavior : CampaignBehaviorBase
{
    public const string PreparationMenuId = "coop_tournament_preparation";
    public const string ActiveMenuId = "coop_tournament_active";

    internal const string TownCenterMenuId = "town";
    internal const string TownArenaMenuId = "town_arena";

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, AddGameMenus);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private static void AddGameMenus(CampaignGameStarter starter)
    {
        if (ModInformation.IsClient)
            AddArenaOptions(starter);

        starter.AddGameMenu(
            PreparationMenuId,
            "{=coop_tournament_preparation_description}Coop tournaments are in BETA. It is recommended to make a save before starting a tournament.\n\nLords in tournament: {TOURNAMENT_LORD_COUNT}\n\nTournament prize: {TOURNAMENT_PRIZE}\n\nEnrolled competitors:\n{TOURNAMENT_PLAYERS}",
            InitializePreparationMenu,
            GameMenu.MenuOverlayType.SettlementWithBoth,
            GameMenu.MenuFlags.None,
            null);

        starter.AddGameMenuOption(
            PreparationMenuId,
            "coop_tournament_start",
            "{=coop_tournament_start}Start tournament",
            StartCondition,
            StartConsequence,
            false,
            0,
            false,
            null);

        starter.AddGameMenuOption(
            PreparationMenuId,
            "coop_tournament_leave_preparation",
            "{=coop_tournament_leave_preparation}Leave tournament",
            LeavePreparationCondition,
            LeavePreparationConsequence,
            true,
            1,
            false,
            null);

        starter.AddGameMenu(
            ActiveMenuId,
            "{=coop_tournament_active_description}The tournament is currently in progress.",
            InitializeActiveMenu,
            GameMenu.MenuOverlayType.SettlementWithBoth,
            GameMenu.MenuFlags.None,
            null);

        starter.AddGameMenuOption(
            ActiveMenuId,
            "coop_tournament_spectate",
            "{=coop_tournament_spectate}Spectate tournament",
            SpectateCondition,
            SpectateConsequence,
            false,
            0,
            false,
            null);

        starter.AddGameMenuOption(
            ActiveMenuId,
            "coop_tournament_back_to_town",
            "{=coop_tournament_back_to_town}Leave tournament",
            BackToTownCondition,
            BackToTownConsequence,
            true,
            1,
            false,
            null);
    }

    private static void AddArenaOptions(CampaignGameStarter starter)
    {
        starter.AddGameMenuOption(
            TownArenaMenuId,
            "coop_join_tournament",
            "{=LN09ZLXZ}Join the tournament",
            ArenaJoinCondition,
            ArenaJoinConsequence,
            false,
            1,
            false,
            null);

        starter.AddGameMenuOption(
            TownArenaMenuId,
            "coop_tournament_leaderboard",
            "{=vGF5S2hE}Leaderboard",
            LeaderboardCondition,
            LeaderboardConsequence,
            false,
            3,
            false,
            null);
    }

    internal static bool IsSupportedTournament(TournamentGame tournamentGame)
        => tournamentGame?.GetType() == typeof(FightTournamentGame);

    private static bool ArenaJoinCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
        if (!TryGetCurrentTournament(out var tournamentGame))
            return false;

        if (!IsSupportedTournament(tournamentGame))
        {
            args.IsEnabled = false;
            args.Tooltip = new TextObject(
                "{=coop_tournament_unsupported}Cooperative tournaments support only the standard Fight Tournament in Bannerlord v1.4.7.");
            return true;
        }

        if (!TryGetTownContext(out var townId, out var controller))
            return false;

        if (controller.TryGetTownSession(townId, out var snapshot))
        {
            if (IsLocalMissionMember(snapshot, controller.LocalControllerId))
                return false;
            if (!snapshot.IsCompleted && snapshot.Phase != TournamentSessionPhase.Preparation)
            {
                args.IsEnabled = true;
                return true;
            }
        }

        args.IsEnabled = true;
        return true;
    }

    private static void ArenaJoinConsequence(MenuCallbackArgs args)
    {
        if (!TryGetTownContext(out var townId, out var controller))
            return;

        if (!controller.TryGetTownSession(townId, out var snapshot) || snapshot.IsCompleted)
        {
            controller.RequestJoin(townId, null, 0);
            return;
        }

        if (snapshot.Phase == TournamentSessionPhase.Preparation)
            controller.RequestJoin(townId, snapshot.SessionId, snapshot.Revision);
        else
            GameMenu.SwitchToMenu(ActiveMenuId);
    }

    private static bool LeaderboardCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Leaderboard;
        return Settlement.CurrentSettlement?.IsTown == true;
    }

    private static void LeaderboardConsequence(MenuCallbackArgs args)
    {
        args.MenuContext?.OpenTournamentLeaderboards();
    }

    private static bool TryGetCurrentTournament(out TournamentGame tournamentGame)
    {
        tournamentGame = null;
        var town = Settlement.CurrentSettlement?.Town;
        if (town == null)
            return false;

        tournamentGame = Campaign.Current?.TournamentManager?.GetTournamentGame(town);
        return tournamentGame != null;
    }

    private static bool IsLocalMissionMember(
        TournamentSessionSnapshot snapshot,
        string controllerId)
    {
        return snapshot.Contestants.Any(contestant =>
                   contestant.IsHuman &&
                   !contestant.IsReplaced &&
                   contestant.ControllerId == controllerId) ||
               snapshot.SpectatorControllerIds.Contains(controllerId);
    }

    private static void InitializePreparationMenu(MenuCallbackArgs args)
    {
        args.MenuTitle = new TextObject("{=coop_tournament_preparation_title}Tournament Preparation");

        if (!TryGetTownContext(out var townId, out var controller)) return;

        int lordCount = controller.TryGetTownSession(townId, out var snapshot)
            ? snapshot.Contestants.Count(contestant => contestant.IsLord)
            : 0;
        MBTextManager.SetTextVariable("TOURNAMENT_LORD_COUNT", lordCount);
        MBTextManager.SetTextVariable("TOURNAMENT_PRIZE", controller.GetPreparationPrizeName(townId));
        MBTextManager.SetTextVariable("TOURNAMENT_PLAYERS", controller.GetPreparationPlayerNames(townId));
    }

    private static void InitializeActiveMenu(MenuCallbackArgs args)
    {
        args.MenuTitle = new TextObject("{=coop_tournament_active_title}Active Tournament");
    }

    private static bool StartCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
        args.IsEnabled = TryGetTownContext(out var townId, out var controller) &&
                         controller.CanStartPreparation(townId);
        return true;
    }

    private static void StartConsequence(MenuCallbackArgs args)
    {
        if (TryGetTownContext(out var townId, out var controller))
            controller.RequestStart(townId);
    }

    private static bool LeavePreparationCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Leave;
        args.IsEnabled = TryGetTownContext(out var townId, out var controller) &&
                         controller.CanLeavePreparation(townId);
        return true;
    }

    private static void LeavePreparationConsequence(MenuCallbackArgs args)
    {
        if (TryGetTownContext(out var townId, out var controller))
            controller.RequestLeavePreparation(townId);
    }

    private static bool SpectateCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Mission;
        args.IsEnabled = TryGetTownContext(out var townId, out var controller) &&
                         controller.CanSpectate(townId);
        return true;
    }

    private static void SpectateConsequence(MenuCallbackArgs args)
    {
        if (TryGetTownContext(out var townId, out var controller))
            controller.RequestSpectate(townId);
    }

    private static bool BackToTownCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Leave;
        return true;
    }

    private static void BackToTownConsequence(MenuCallbackArgs args)
    {
        GameMenu.SwitchToMenu(TownCenterMenuId);
    }

    private static bool TryGetTownContext(out string townId, out ITournamentUIController controller)
    {
        townId = null;
        controller = null;

        var town = Settlement.CurrentSettlement?.Town;
        if (town == null ||
            !ContainerProvider.TryResolve<TournamentUIController>(out var resolvedController) ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetId(town, out townId))
            return false;

        controller = resolvedController;
        return true;
    }
}
