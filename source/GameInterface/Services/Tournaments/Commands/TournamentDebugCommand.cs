using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using GameInterface.Services.Tournaments.UI;
using SandBox.Tournaments.MissionLogics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Tournaments.Commands;

public class TournamentDebugCommand
{
#if DEBUG
    private const string DanusticaSettlementId = "town_ES1";
    private static DanusticaTournamentFixture fixture;

    private sealed class DanusticaTournamentFixture
    {
        public Campaign Campaign;
        public TournamentManager Manager;
        public TournamentGame CreatedGame;
    }
#endif
    public static string AddTournamentToTown(List<string> args)
    {
        if (ModInformation.IsClient)
            return "This function can only be used by the server";


        if (Campaign.Current?.TournamentManager is not TournamentManager tournamentManager)
            return "No campaign is currently loaded";

        if (!TryResolveTown(args[0], out var town))
            return $"Town '{args[0]}' not found";

        if (tournamentManager.GetTournamentGame(town) != null)
            return $"{town.Name} already has an active tournament";

        bool tournamentAdded = false;
        GameThread.RunSafe(
            () =>
            {
                tournamentManager.AddTournament(new FightTournamentGame(town));
                tournamentAdded = true;
            },
            blocking: true,
            context: nameof(AddTournamentToTown));

        return tournamentAdded
            ? $"Added a tournament to {town.Name}"
            : $"Failed to add a tournament to {town.Name}; check the log for details";
    }

#if DEBUG
    public static string BeginDanusticaFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";
        if (fixture != null)
            return "A Danustica tournament fixture is already pending restoration.";
        if (!TryResolveDanusticaContext(out var town, out var townId, out var error))
            return error;
        if (Campaign.Current?.TournamentManager is not TournamentManager tournamentManager)
            return "No campaign is currently loaded.";
        if (!ContainerProvider.TryResolve<ITournamentSessionRegistry>(out var registry))
            return "Unable to resolve the tournament session registry.";
        if (registry.TryGetByTown(townId, out var openSession))
        {
            return
                $"Danustica already has coop session {openSession.SessionId} in phase {openSession.Phase}.";
        }

        TournamentGame tournamentGame = tournamentManager.GetTournamentGame(town);
        if (tournamentGame != null &&
            !CoopTournamentCampaignBehavior.IsSupportedTournament(tournamentGame))
        {
            return
                $"Danustica has unsupported tournament type {tournamentGame.GetType().Name}.";
        }

        TournamentGame createdGame = null;
        if (tournamentGame == null)
        {
            createdGame = new FightTournamentGame(town);
            tournamentManager.AddTournament(createdGame);
            tournamentGame = createdGame;
        }

        fixture = new DanusticaTournamentFixture
        {
            Campaign = Campaign.Current,
            Manager = tournamentManager,
            CreatedGame = createdGame,
        };

        return
            $"DANUSTICA_TOURNAMENT_FIXTURE_STARTED townId={townId}|" +
            $"nativeType={tournamentGame.GetType().Name}|created={createdGame != null}";
    }
    public static string DanusticaFixtureState(List<string> args)
    {

        string fixtureState;
        if (fixture == null)
        {
            fixtureState = "none";
        }
        else if (fixture.Campaign != Campaign.Current)
        {
            fixtureState = "stale-campaign";
        }
        else
        {
            bool createdGameActive =
                fixture.CreatedGame != null &&
                fixture.Manager._activeTournaments.Contains(fixture.CreatedGame);
            fixtureState =
                $"active|created={fixture.CreatedGame != null}|createdGameActive={createdGameActive}";
        }

        return $"DANUSTICA_TOURNAMENT_FIXTURE state={fixtureState}\n" + ObserveDanustica();
    }
    public static string RestoreDanusticaFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";
        if (fixture == null)
            return "No Danustica tournament fixture is pending restoration.";

        DanusticaTournamentFixture activeFixture = fixture;
        if (activeFixture.Campaign != Campaign.Current)
        {
            fixture = null;
            return "The Danustica tournament fixture belonged to a previous campaign and was discarded.";
        }
        if (!TryResolveDanusticaContext(out _, out var townId, out var error))
            return error;
        if (!ContainerProvider.TryResolve<ITournamentSessionRegistry>(out var registry))
            return "Unable to resolve the tournament session registry.";
        if (registry.TryGetByTown(townId, out var openSession))
        {
            return
                $"Cannot restore while coop session {openSession.SessionId} is open in phase {openSession.Phase}.";
        }

        TournamentGame createdGame = activeFixture.CreatedGame;
        if (createdGame != null &&
            activeFixture.Manager._activeTournaments.Contains(createdGame))
        {
            activeFixture.Manager.RemoveTournament(createdGame);
            if (activeFixture.Manager._activeTournaments.Contains(createdGame))
                return "The fixture-created Danustica tournament could not be removed.";
        }

        fixture = null;
        return
            $"DANUSTICA_TOURNAMENT_FIXTURE_RESTORED removedCreatedTournament={createdGame != null}";
    }
    public static string AbortDanusticaFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";
        if (fixture == null)
            return "No Danustica tournament fixture is pending restoration.";
        if (fixture.Campaign != Campaign.Current)
        {
            fixture = null;
            return "The Danustica tournament fixture belonged to a previous campaign and was discarded.";
        }
        if (fixture.CreatedGame == null)
            return "Refusing to abort a tournament that was not created by the fixture.";
        if (!TryResolveDanusticaContext(out _, out var townId, out var error))
            return error;
        if (!ContainerProvider.TryResolve<ITournamentSessionRegistry>(out var registry) ||
            !ContainerProvider.TryResolve<INetwork>(out var network) ||
            !ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker))
        {
            return "Unable to resolve the tournament fixture cleanup services.";
        }

        if (!registry.TryGetByTown(townId, out var session))
            return "The fixture has no active Danustica session to abort.";
        if (!registry.Remove(session.SessionId))
            return $"Unable to remove Danustica session {session.SessionId}.";

        var removal = new NetworkTournamentSessionRemoved(session.SessionId, townId);
        network.SendAll(removal);
        messageBroker.Publish(
            typeof(TournamentDebugCommand),
            new TournamentSessionRemoved(session.SessionId, townId));
        return
            $"DANUSTICA_TOURNAMENT_FIXTURE_ABORTED sessionId={session.SessionId}|" +
            $"phase={session.Phase}|createdTournamentPreservedForRestore=True";
    }
    public static string RequestDanusticaJoin(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";
        if (!TryResolveDanusticaController(out var controller, out _, out var townId, out var error))
            return error;

        if (!controller.TryGetTownSession(townId, out var snapshot) || snapshot.IsCompleted)
        {
            controller.RequestJoin(townId, null, 0);
            return $"DANUSTICA_TOURNAMENT_JOIN_REQUESTED townId={townId}|sessionId=none|revision=0";
        }
        if (snapshot.Phase != TournamentSessionPhase.Preparation)
        {
            return
                $"Danustica session {snapshot.SessionId} is in phase {snapshot.Phase}, not Preparation.";
        }

        controller.RequestJoin(townId, snapshot.SessionId, snapshot.Revision);
        return
            $"DANUSTICA_TOURNAMENT_JOIN_REQUESTED townId={townId}|" +
            $"sessionId={snapshot.SessionId}|revision={snapshot.Revision}";
    }
    public static string RequestDanusticaStart(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";
        if (!TryResolveDanusticaController(out var controller, out _, out var townId, out var error))
            return error;
        if (!controller.TryGetTownSession(townId, out var snapshot))
            return "The client has no Danustica tournament session snapshot.";
        if (snapshot.Phase != TournamentSessionPhase.Preparation)
            return $"Danustica session {snapshot.SessionId} is in phase {snapshot.Phase}, not Preparation.";

        controller.RequestStart(townId);
        return
            $"DANUSTICA_TOURNAMENT_START_REQUESTED sessionId={snapshot.SessionId}|revision={snapshot.Revision}";
    }
    public static string RequestDanusticaChoice(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";
        if (!Enum.TryParse(args[0], true, out TournamentPlayerChoice choice) ||
            choice == TournamentPlayerChoice.None)
        {
            return "Usage: coop.debug.tournaments.danustica_request_choice <Join|Watch|Skip>";
        }
        if (!TryResolveDanusticaController(out var controller, out _, out var townId, out var error))
            return error;
        if (!controller.TryGetTownSession(townId, out var snapshot))
            return "The client has no Danustica tournament session snapshot.";
        if (snapshot.Phase != TournamentSessionPhase.AwaitingChoices ||
            string.IsNullOrEmpty(snapshot.CurrentMatchId))
        {
            return
                $"Danustica session {snapshot.SessionId} is not awaiting a match choice; " +
                $"phase={snapshot.Phase}|matchId={snapshot.CurrentMatchId ?? "none"}.";
        }

        controller.RequestChoice(snapshot, choice);
        return
            $"DANUSTICA_TOURNAMENT_CHOICE_REQUESTED sessionId={snapshot.SessionId}|" +
            $"revision={snapshot.Revision}|matchId={snapshot.CurrentMatchId}|choice={choice}";
    }
    public static string RequestDanusticaLeave(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";
        if (!TryResolveDanusticaController(out var controller, out _, out var townId, out var error))
            return error;
        if (!controller.TryGetTownSession(townId, out var snapshot))
            return "The client has no Danustica tournament session snapshot.";

        controller.RequestLeaveActive(snapshot);
        return
            $"DANUSTICA_TOURNAMENT_LEAVE_REQUESTED sessionId={snapshot.SessionId}|" +
            $"revision={snapshot.Revision}|phase={snapshot.Phase}";
    }
    public static string ObserveDanusticaCommand(List<string> args)
    {

        return ObserveDanustica();
    }

    private static string ObserveDanustica()
    {
        if (!TryResolveDanusticaContext(out var town, out var townId, out var error))
            return error;

        TournamentGame nativeGame = Campaign.Current?.TournamentManager?.GetTournamentGame(town);
        TournamentSessionSnapshot snapshot = null;
        string sessionSource;
        string localControllerId = null;
        if (ModInformation.IsServer)
        {
            sessionSource = "server-registry";
            if (ContainerProvider.TryResolve<ITournamentSessionRegistry>(out var registry))
                registry.TryGetByTown(townId, out snapshot);
        }
        else
        {
            sessionSource = "client-ui";
            if (ContainerProvider.TryResolve<TournamentUIController>(out var controller))
            {
                localControllerId = controller.LocalControllerId;
                controller.TryGetTownSession(townId, out snapshot);
            }
        }

        var output = new StringBuilder();
        output.AppendLine(
            $"DANUSTICA_TOURNAMENT_OBSERVATION role={(ModInformation.IsServer ? "server" : "client")}|" +
            $"townId={townId}|nativeType={nativeGame?.GetType().Name ?? "none"}|" +
            $"sessionSource={sessionSource}|localControllerId={localControllerId ?? "none"}|" +
            $"encounterSettlement={PlayerEncounter.EncounterSettlement?.StringId ?? "none"}");
        AppendSessionState(output, snapshot, localControllerId);
        AppendMissionState(output);
        return output.ToString().TrimEnd();
    }

    private static void AppendSessionState(
        StringBuilder output,
        TournamentSessionSnapshot snapshot,
        string localControllerId)
    {
        if (snapshot == null)
        {
            output.AppendLine("session=none");
            return;
        }

        string localChoice = "none";
        if (!string.IsNullOrEmpty(localControllerId))
        {
            CoopTournamentVM.UIState state = CoopTournamentVM.CalculateUIState(
                snapshot,
                localControllerId,
                false);
            if (state.CanJoin)
                localChoice = TournamentPlayerChoice.Join.ToString();
            else if (state.CanWatch)
                localChoice = TournamentPlayerChoice.Watch.ToString();
        }

        int humans = snapshot.Contestants.Count(contestant =>
            contestant.IsHuman && !contestant.IsReplaced);
        string choices = string.Join(
            ",",
            snapshot.Choices
                .OrderBy(value => value.ControllerId, StringComparer.Ordinal)
                .Select(value => $"{value.ControllerId}:{value.Choice}"));
        output.AppendLine(
            $"session={snapshot.SessionId}|phase={snapshot.Phase}|revision={snapshot.Revision}|" +
            $"bracketRevision={snapshot.BracketRevision}|matchId={snapshot.CurrentMatchId ?? "none"}|" +
            $"host={snapshot.HostControllerId ?? "none"}|localChoice={localChoice}|humans={humans}|" +
            $"spectators={snapshot.SpectatorControllerIds.Length}|ready={snapshot.ReadyCount}|" +
            $"skip={snapshot.SkipCount}|voters={snapshot.VoterCount}|choices={choices}");
    }

    private static void AppendMissionState(StringBuilder output)
    {
        Mission mission = Mission.Current ?? MissionState.Current?.CurrentMission;
        if (mission == null)
        {
            output.AppendLine("mission=none");
            return;
        }

        string tournamentBehaviors = string.Join(
            ",",
            mission.MissionBehaviors
                .Select(behavior => behavior.GetType().Name)
                .Where(name => name.IndexOf("Tournament", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(name => name, StringComparer.Ordinal));
        TournamentBehavior tournamentBehavior = mission.GetMissionBehavior<TournamentBehavior>();
        output.AppendLine(
            $"mission=active|scene={mission.SceneName}|mode={mission.Mode}|" +
            $"ending={mission.IsMissionEnding}|agents={mission.Agents.Count}|" +
            $"mainAgent={mission.MainAgent?.Name ?? "none"}|" +
            $"nativeBracketReady={IsNativeBracketReady(tournamentBehavior)}|" +
            $"tournamentBehaviors={tournamentBehaviors}");
    }

    private static bool IsNativeBracketReady(TournamentBehavior behavior)
    {
        if (behavior?.Rounds == null ||
            behavior.CurrentRoundIndex < 0 ||
            behavior.CurrentRoundIndex >= behavior.Rounds.Length)
        {
            return false;
        }

        TournamentRound round = behavior.Rounds[behavior.CurrentRoundIndex];
        return round?.Matches != null &&
               round.CurrentMatchIndex >= 0 &&
               round.CurrentMatchIndex < round.Matches.Length &&
               round.Matches[round.CurrentMatchIndex] != null;
    }

    private static bool TryResolveDanusticaController(
        out TournamentUIController controller,
        out Town town,
        out string townId,
        out string error)
    {
        controller = null;
        if (!TryResolveDanusticaContext(out town, out townId, out error))
            return false;
        if (!ContainerProvider.TryResolve(out controller))
        {
            error = "Unable to resolve the tournament UI controller.";
            return false;
        }

        return true;
    }

    private static bool TryResolveDanusticaContext(
        out Town town,
        out string townId,
        out string error)
    {
        townId = null;
        if (!TryResolveTown(DanusticaSettlementId, out town))
        {
            error = "Unable to resolve Danustica (town_ES1).";
            return false;
        }
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetId(town, out townId))
        {
            error = "Unable to resolve Danustica's registered town id.";
            return false;
        }

        error = null;
        return true;
    }
#endif

    private static bool TryResolveTown(string townIdentifier, out Town town)
    {
        town = Campaign.Current?.CampaignObjectManager?.Settlements
            .Where(settlement => settlement.IsTown)
            .FirstOrDefault(settlement =>
                string.Equals(settlement.StringId, townIdentifier, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(settlement.Town?.StringId, townIdentifier, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(settlement.Name?.ToString(), townIdentifier, StringComparison.OrdinalIgnoreCase))
            ?.Town;
        return town != null;
    }
}
