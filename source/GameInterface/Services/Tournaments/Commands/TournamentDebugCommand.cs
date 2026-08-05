using Common;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Tournaments.Commands;

public class TournamentDebugCommand
{
    [CommandLineArgumentFunction("add_tournament_to_town", "coop.debug.tournaments")]
    public static string AddTournamentToTown(List<string> args)
    {
        if (ModInformation.IsClient)
            return "This function can only be used by the server";

        if (args.Count != 1)
            return "Usage: coop.debug.tournaments.add_tournament_to_town <town name or id>";

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
    [CommandLineArgumentFunction("remove_tournament_from_town", "coop.debug.tournaments")]
    public static string RemoveTournamentFromTown(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 1)
            return "Usage: coop.debug.tournaments.remove_tournament_from_town <town name or id>";

        if (Campaign.Current?.TournamentManager is not TournamentManager tournamentManager)
            return "No campaign is currently loaded.";
        if (!TryResolveTown(args[0], out var town))
            return $"Town '{args[0]}' not found.";

        var tournament = tournamentManager.GetTournamentGame(town);
        if (tournament == null)
            return $"{town.Name} has no active tournament.";

        tournamentManager.RemoveTournament(tournament);
        return $"Removed the tournament from {town.Name}.";
    }

    [CommandLineArgumentFunction("enter_town", "coop.debug.tournaments")]
    public static string EnterTown(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 2)
            return "Usage: coop.debug.tournaments.enter_town <controllerId> <town name or id>";

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            return "Unable to resolve the player services.";
        }

        if (!playerManager.TryGetPlayer(args[0], out var player) || !playerManager.IsConnected(player))
            return $"No connected player has controller id {args[0]}.";

        if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var playerParty))
            return $"Unable to resolve player party {player.MobilePartyId}.";

        if (!TryResolveTown(args[1], out var town))
            return $"Town '{args[1]}' not found.";

        if (playerParty.CurrentSettlement == town.Settlement)
            return $"Player {args[0]} is already in {town.Name} ({town.Settlement.StringId}).";
        if (playerParty.CurrentSettlement != null)
            return $"Player {args[0]} is already in {playerParty.CurrentSettlement.Name}.";
        if (playerParty.MapEvent != null)
            return $"Player {args[0]} is in a map event and cannot enter {town.Name}.";

        EnterSettlementAction.ApplyForParty(playerParty, town.Settlement);
        return playerParty.CurrentSettlement == town.Settlement
            ? $"Moved player {args[0]} into {town.Name} ({town.Settlement.StringId})."
            : $"Player {args[0]} did not enter {town.Name}; check the log for details.";
    }

    [CommandLineArgumentFunction("join", "coop.debug.tournaments")]
    public static string Join(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 1)
            return "Usage: coop.debug.tournaments.join <town name or id>";

        if (!TryResolveClientTown(args[0], out var townId, out var town, out var controller, out var error))
            return error;

        if (Settlement.CurrentSettlement?.Town != town)
            return $"The local player is not in {town.Name} ({town.Settlement.StringId}).";

        if (!controller.TryGetTownSession(townId, out var snapshot) || snapshot.IsCompleted)
        {
            controller.RequestJoin(townId, null, 0);
            return $"Requested a new tournament session in {town.Name} ({townId}).";
        }

        if (IsLocalContestant(snapshot, controller.LocalControllerId))
            return $"The local player already joined tournament session {snapshot.SessionId}.";
        if (snapshot.Phase != TournamentSessionPhase.Preparation)
            return $"Tournament session {snapshot.SessionId} is already in phase {snapshot.Phase}.";

        controller.RequestJoin(townId, snapshot.SessionId, snapshot.Revision);
        return $"Requested tournament join: town={townId}, session={snapshot.SessionId}, revision={snapshot.Revision}.";
    }

    [CommandLineArgumentFunction("start", "coop.debug.tournaments")]
    public static string Start(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 1)
            return "Usage: coop.debug.tournaments.start <town name or id>";

        if (!TryResolveClientTown(args[0], out var townId, out _, out var controller, out var error))
            return error;
        if (!controller.TryGetTownSession(townId, out var snapshot))
            return $"No tournament session is known for {townId}.";
        if (!controller.CanStartPreparation(townId))
            return $"Tournament session {snapshot.SessionId} cannot be started locally in phase {snapshot.Phase}.";

        controller.RequestStart(townId);
        return $"Requested tournament start: town={townId}, session={snapshot.SessionId}, revision={snapshot.Revision}.";
    }

    [CommandLineArgumentFunction("leave_preparation", "coop.debug.tournaments")]
    public static string LeavePreparation(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 1)
            return "Usage: coop.debug.tournaments.leave_preparation <town name or id>";

        if (!TryResolveClientTown(args[0], out var townId, out _, out var controller, out var error))
            return error;
        if (!controller.TryGetTownSession(townId, out var snapshot))
            return $"No tournament session is known for {townId}.";
        if (!controller.CanLeavePreparation(townId))
            return $"Tournament session {snapshot.SessionId} cannot be left locally in phase {snapshot.Phase}.";

        controller.RequestLeavePreparation(townId);
        return $"Requested tournament preparation leave: town={townId}, session={snapshot.SessionId}, revision={snapshot.Revision}.";
    }

    [CommandLineArgumentFunction("leave_mission", "coop.debug.tournaments")]
    public static string LeaveMission(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 0)
            return "Usage: coop.debug.tournaments.leave_mission";

        var mission = Mission.Current;
        if (mission == null)
            return "No mission is active.";

        mission.EndMission();
        return "Ending the local tournament mission.";
    }

    [CommandLineArgumentFunction("state", "coop.debug.tournaments")]
    public static string State(List<string> args)
    {
        if (args.Count != 1)
            return "Usage: coop.debug.tournaments.state <town name or id>";

        if (!TryResolveTown(args[0], out var town))
            return $"Town '{args[0]}' not found.";

        var nativeTournament = Campaign.Current?.TournamentManager?.GetTournamentGame(town) != null;
        var controllerId = "server";
        var sessionId = "none";
        var phase = "none";
        long revision = 0;
        var localContestant = false;
        var townId = town.StringId;

        if (ContainerProvider.TryResolve<TournamentUIController>(out var controller))
        {
            controllerId = controller.LocalControllerId ?? "unknown";
            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) &&
                objectManager.TryGetId(town, out string resolvedTownId))
            {
                townId = resolvedTownId;
            }

            if (controller.TryGetTownSession(townId, out var snapshot))
            {
                sessionId = snapshot.SessionId;
                phase = snapshot.Phase.ToString();
                revision = snapshot.Revision;
                localContestant = IsLocalContestant(snapshot, controller.LocalControllerId);
            }
        }

        var mission = Mission.Current;
        var currentSettlementId = Settlement.CurrentSettlement?.StringId ?? "none";
        return $"Tournament state: role={(ModInformation.IsServer ? "server" : "client")}, " +
               $"controller={controllerId}, town={townId}, settlement={currentSettlementId}, " +
               $"nativeTournament={nativeTournament}, session={sessionId}, phase={phase}, revision={revision}, " +
               $"localContestant={localContestant}, missionActive={mission != null}, missionAgents={mission?.Agents.Count ?? 0}.";
    }

    private static bool TryResolveClientTown(
        string townIdentifier,
        out string townId,
        out Town town,
        out TournamentUIController controller,
        out string error)
    {
        townId = null;
        town = null;
        controller = null;
        error = null;

        if (!TryResolveTown(townIdentifier, out town))
        {
            error = $"Town '{townIdentifier}' not found.";
            return false;
        }

        if (!ContainerProvider.TryResolve<TournamentUIController>(out controller) ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            error = "Unable to resolve the tournament UI services.";
            return false;
        }

        if (!objectManager.TryGetId(town, out townId))
        {
            error = $"Unable to resolve the network id for {town.Name}.";
            return false;
        }

        return true;
    }

    private static bool IsLocalContestant(TournamentSessionSnapshot snapshot, string controllerId)
        => snapshot?.Contestants.Any(contestant =>
            contestant.IsHuman &&
            !contestant.IsReplaced &&
            contestant.ControllerId == controllerId) == true;
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
