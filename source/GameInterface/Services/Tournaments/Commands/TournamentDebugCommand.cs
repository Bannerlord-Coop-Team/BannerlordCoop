using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using GameInterface.Services.Tournaments.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Tournaments.Commands;

public class TournamentDebugCommand
{
#if DEBUG
    private static TournamentCampaignFixture campaignFixture;
    private static PlayerEncounter originalCombatFixtureEncounter;
    private static PlayerEncounter installedCombatFixtureEncounter;
    private static string combatFixtureEncounterTownId;
#endif

    [CommandLineArgumentFunction("add_tournament_to_town", "coop.debug.tournaments")]
    public static string AddTournamentToTown(List<string> args)
    {
        if (ModInformation.IsClient)
            return "This function can only be used by the server";

        if (args.Count != 1)
            return "Usage: coop.debug.tournaments.add_tournament_to_town <town name or id>";

        if (Campaign.Current?.TournamentManager is not TournamentManager tournamentManager)
            return "No campaign is currently loaded";

        string townIdentifier = args[0];
        Town town = Campaign.Current.CampaignObjectManager.Settlements
            .Where(settlement => settlement.IsTown)
            .FirstOrDefault(settlement =>
                string.Equals(settlement.StringId, townIdentifier, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(settlement.Town?.StringId, townIdentifier, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(settlement.Name?.ToString(), townIdentifier, StringComparison.OrdinalIgnoreCase))
            ?.Town;
        if (town == null)
            return $"Town '{townIdentifier}' not found";

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
    [CommandLineArgumentFunction("combat_fixture_prepare_town", "coop.debug.tournaments")]
    public static string PrepareCombatFixtureTown(List<string> args)
    {
        const string usage =
            "Usage: coop.debug.tournaments.combat_fixture_prepare_town town_ES1 player1-controller-id player2-controller-id";
        if (ModInformation.IsClient)
            return "This function can only be used by the server";
        if (args.Count != 3 || args[1] == args[2])
            return usage;
        if (campaignFixture != null)
            return "A tournament campaign fixture is already active";
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
            return "Unable to resolve PlayerManager";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve ObjectManager";
        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
            return "Unable to resolve MobilePartyBehaviorSnapshot";

        Settlement settlement = Campaign.Current?.CampaignObjectManager?.Settlements
            .FirstOrDefault(candidate =>
                string.Equals(candidate.StringId, args[0], StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.Name?.ToString(), args[0], StringComparison.OrdinalIgnoreCase));
        Town town = settlement?.Town;
        if (town == null)
            return $"Tournament town '{args[0]}' was not found";
        if (!objectManager.TryGetId(town, out var townId))
            return $"Unable to resolve the network id for {town.Name}";
        if (!TryResolveFixtureParticipant(
                args[1],
                playerManager,
                objectManager,
                behaviorSnapshot,
                out TournamentCampaignParticipant playerOne,
                out var error))
            return error;
        if (!TryResolveFixtureParticipant(
                args[2],
                playerManager,
                objectManager,
                behaviorSnapshot,
                out TournamentCampaignParticipant playerTwo,
                out error))
            return error;

        TournamentManager tournamentManager = Campaign.Current?.TournamentManager as TournamentManager;
        if (tournamentManager == null)
            return "No campaign tournament manager is available";

        bool tournamentAdded = tournamentManager.GetTournamentGame(town) == null;
        try
        {
            if (tournamentAdded)
                tournamentManager.AddTournament(new FightTournamentGame(town));

            EnterSettlementAction.ApplyForParty(playerOne.Party, settlement);
            EnterSettlementAction.ApplyForParty(playerTwo.Party, settlement);
            if (playerOne.Party.CurrentSettlement != settlement ||
                playerTwo.Party.CurrentSettlement != settlement)
            {
                RestoreParticipant(playerOne, behaviorSnapshot);
                RestoreParticipant(playerTwo, behaviorSnapshot);
                if (tournamentAdded)
                {
                    TournamentGame tournament = tournamentManager.GetTournamentGame(town);
                    if (tournament != null)
                        tournamentManager.RemoveTournament(tournament);
                }
                return $"Failed to move both player parties into {town.Name}";
            }
        }
        catch (Exception exception)
        {
            RestoreParticipant(playerOne, behaviorSnapshot);
            RestoreParticipant(playerTwo, behaviorSnapshot);
            TournamentGame tournament = tournamentManager.GetTournamentGame(town);
            if (tournamentAdded && tournament != null)
                tournamentManager.RemoveTournament(tournament);
            return $"Failed to prepare the tournament campaign fixture: {exception.Message}";
        }

        campaignFixture = new TournamentCampaignFixture(
            town,
            townId,
            tournamentAdded,
            playerOne,
            playerTwo);
        return $"TOURNAMENT_TOWN_READY name={town.Name} settlement={settlement.StringId} townId={townId} " +
            $"player1={playerOne.ControllerId} party1={playerOne.Party.StringId} " +
            $"origin1={FormatPosition(playerOne.OriginalPosition)} " +
            $"player2={playerTwo.ControllerId} party2={playerTwo.Party.StringId} " +
            $"origin2={FormatPosition(playerTwo.OriginalPosition)}";
    }

    [CommandLineArgumentFunction("combat_fixture_restore_town", "coop.debug.tournaments")]
    public static string RestoreCombatFixtureTown(List<string> args)
    {
        if (ModInformation.IsClient)
            return "This function can only be used by the server";
        if (args.Count != 0)
            return "Usage: coop.debug.tournaments.combat_fixture_restore_town";
        if (campaignFixture == null)
            return "TOURNAMENT_TOWN_RESTORED fixtureActive=false";
        if (ContainerProvider.TryResolve<ITournamentSessionRegistry>(out var sessionRegistry) &&
            sessionRegistry.TryGetByTown(campaignFixture.TownId, out var snapshot))
        {
            return $"TOURNAMENT_TOWN_RESTORE_PENDING phase={snapshot.Phase} revision={snapshot.Revision}";
        }

        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
            return "TOURNAMENT_TOWN_RESTORE_PENDING behaviorSnapshotUnavailable=true";
        bool playerOneRestored = RestoreParticipant(campaignFixture.PlayerOne, behaviorSnapshot);
        bool playerTwoRestored = RestoreParticipant(campaignFixture.PlayerTwo, behaviorSnapshot);
        if (!playerOneRestored || !playerTwoRestored)
        {
            return $"TOURNAMENT_TOWN_RESTORE_PENDING " +
                $"player1Restored={playerOneRestored} player2Restored={playerTwoRestored}";
        }

        TournamentManager tournamentManager = Campaign.Current?.TournamentManager as TournamentManager;
        TournamentGame tournament = tournamentManager?.GetTournamentGame(campaignFixture.Town);
        if (campaignFixture.TournamentAdded && tournament != null)
            tournamentManager.RemoveTournament(tournament);
        if (campaignFixture.TournamentAdded &&
            tournamentManager?.GetTournamentGame(campaignFixture.Town) != null)
        {
            return "TOURNAMENT_TOWN_RESTORE_PENDING tournamentStillActive=true";
        }

        string result =
            $"TOURNAMENT_TOWN_RESTORED fixtureActive=false " +
            $"player1={campaignFixture.PlayerOne.ControllerId} " +
            $"position1={FormatPosition(campaignFixture.PlayerOne.Party.Position)} " +
            $"sea1={campaignFixture.PlayerOne.Party.IsCurrentlyAtSea} " +
            $"behavior1={campaignFixture.PlayerOne.Party.DefaultBehavior} " +
            $"player2={campaignFixture.PlayerTwo.ControllerId} " +
            $"position2={FormatPosition(campaignFixture.PlayerTwo.Party.Position)} " +
            $"sea2={campaignFixture.PlayerTwo.Party.IsCurrentlyAtSea} " +
            $"behavior2={campaignFixture.PlayerTwo.Party.DefaultBehavior}";
        campaignFixture = null;
        return result;
    }

    [CommandLineArgumentFunction("combat_fixture_join", "coop.debug.tournaments")]
    public static string JoinCombatFixtureTournament(List<string> args)
    {
        if (ModInformation.IsServer)
            return "This function can only be used by a client";
        if (args.Count != 1)
            return "Usage: coop.debug.tournaments.combat_fixture_join town-id";
        if (!ContainerProvider.TryResolve<TournamentUIController>(out var controller))
            return "Unable to resolve TournamentUIController";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve ObjectManager";
        if (!objectManager.TryGetObject(args[0], out Town town))
            return $"Tournament town '{args[0]}' was not found";

        string encounterError = PrepareCombatFixtureEncounter(args[0], town);
        if (encounterError != null)
            return encounterError;

        controller.RequestJoin(args[0], null, 0);
        return $"TOURNAMENT_JOIN_REQUESTED local={controller.LocalControllerId} townId={args[0]}";
    }

    [CommandLineArgumentFunction("combat_fixture_start", "coop.debug.tournaments")]
    public static string StartCombatFixtureTournament(List<string> args)
    {
        if (ModInformation.IsServer)
            return "This function can only be used by a client";
        if (args.Count != 1)
            return "Usage: coop.debug.tournaments.combat_fixture_start town-id";
        if (!ContainerProvider.TryResolve<TournamentUIController>(out var controller))
            return "Unable to resolve TournamentUIController";
        if (!controller.TryGetTownSession(args[0], out var snapshot))
            return $"TOURNAMENT_SESSION_PENDING townId={args[0]}";
        if (!controller.CanStartPreparation(args[0]))
            return $"TOURNAMENT_START_NOT_READY phase={snapshot.Phase} revision={snapshot.Revision}";

        controller.RequestStart(args[0]);
        return $"TOURNAMENT_START_REQUESTED local={controller.LocalControllerId} " +
            $"session={snapshot.SessionId} revision={snapshot.Revision}";
    }

    [CommandLineArgumentFunction("combat_fixture_vote", "coop.debug.tournaments")]
    public static string VoteCombatFixtureTournament(List<string> args)
    {
        if (ModInformation.IsServer)
            return "This function can only be used by a client";
        if (args.Count != 2 ||
            (args[1] != "live" && args[1] != "skip"))
            return "Usage: coop.debug.tournaments.combat_fixture_vote town-id live|skip";
        if (!ContainerProvider.TryResolve<TournamentUIController>(out var controller))
            return "Unable to resolve TournamentUIController";
        if (!controller.TryGetTownSession(args[0], out var snapshot))
            return $"TOURNAMENT_SESSION_PENDING townId={args[0]}";
        if (snapshot.Phase != TournamentSessionPhase.AwaitingChoices)
            return $"TOURNAMENT_VOTE_NOT_READY phase={snapshot.Phase} revision={snapshot.Revision}";

        TournamentPlayerChoice choice;
        if (args[1] == "skip")
        {
            choice = TournamentPlayerChoice.Skip;
        }
        else
        {
            TournamentMatchData match = GetCurrentMatch(snapshot);
            string localSlotId = snapshot.Contestants
                .FirstOrDefault(contestant =>
                    contestant.IsHuman &&
                    !contestant.IsReplaced &&
                    contestant.ControllerId == controller.LocalControllerId)
                ?.SlotId;
            bool localIsCurrentCompetitor =
                match?.Teams?.SelectMany(team => team.ParticipantSlotIds)
                    .Contains(localSlotId) == true;
            choice = localIsCurrentCompetitor
                ? TournamentPlayerChoice.Join
                : TournamentPlayerChoice.Watch;
        }

        controller.RequestChoice(snapshot, choice);
        return $"TOURNAMENT_VOTE_REQUESTED local={controller.LocalControllerId} " +
            $"choice={choice} match={snapshot.CurrentMatchId} revision={snapshot.Revision}";
    }

    [CommandLineArgumentFunction("combat_fixture_session_state", "coop.debug.tournaments")]
    public static string GetCombatFixtureSessionState(List<string> args)
    {
        if (ModInformation.IsServer)
            return "This function can only be used by a client";
        if (args.Count != 1)
            return "Usage: coop.debug.tournaments.combat_fixture_session_state town-id";
        if (!ContainerProvider.TryResolve<TournamentUIController>(out var controller))
            return "Unable to resolve TournamentUIController";
        if (!controller.TryGetTownSession(args[0], out var snapshot))
            return $"TOURNAMENT_SESSION_PENDING townId={args[0]}";

        TournamentMatchData match = GetCurrentMatch(snapshot);
        var currentSlotIds = new HashSet<string>(
            match?.Teams?.SelectMany(team => team.ParticipantSlotIds) ??
            Array.Empty<string>());
        string activeControllers = string.Join(",",
            snapshot.Contestants
                .Where(contestant =>
                    contestant.IsHuman &&
                    !contestant.IsReplaced &&
                    currentSlotIds.Contains(contestant.SlotId))
                .Select(contestant => contestant.ControllerId));
        if (activeControllers.Length == 0)
            activeControllers = "none";

        return $"TOURNAMENT_SESSION phase={snapshot.Phase} revision={snapshot.Revision} " +
            $"match={snapshot.CurrentMatchId ?? "none"} host={snapshot.HostControllerId ?? "none"} " +
            $"activeControllers={activeControllers} skipAllowed={snapshot.SkipAllowed} " +
            $"ready={snapshot.ReadyCount}/{snapshot.VoterCount} " +
            $"skip={snapshot.SkipCount}/{snapshot.VoterCount} local={controller.LocalControllerId}";
    }

    [CommandLineArgumentFunction("combat_fixture_leave", "coop.debug.tournaments")]
    public static string LeaveCombatFixtureTournament(List<string> args)
    {
        if (ModInformation.IsServer)
            return "This function can only be used by a client";
        if (args.Count != 1)
            return "Usage: coop.debug.tournaments.combat_fixture_leave town-id";
        if (!ContainerProvider.TryResolve<TournamentUIController>(out var controller))
            return "Unable to resolve TournamentUIController";
        if (!controller.TryGetTownSession(args[0], out var snapshot))
            return $"TOURNAMENT_LEFT local={controller.LocalControllerId}";

        controller.RequestLeaveActive(snapshot);
        return $"TOURNAMENT_LEAVE_REQUESTED local={controller.LocalControllerId} " +
            $"session={snapshot.SessionId} revision={snapshot.Revision}";
    }

    [CommandLineArgumentFunction("combat_fixture_restore_encounter", "coop.debug.tournaments")]
    public static string RestoreCombatFixtureEncounter(List<string> args)
    {
        if (ModInformation.IsServer)
            return "This function can only be used by a client";
        if (args.Count != 0)
            return "Usage: coop.debug.tournaments.combat_fixture_restore_encounter";
        if (installedCombatFixtureEncounter == null)
            return "TOURNAMENT_ENCOUNTER_RESTORED fixtureActive=false";
        if (Mission.Current != null)
            return "TOURNAMENT_ENCOUNTER_RESTORE_PENDING missionActive=true";
        if (Campaign.Current == null)
            return "TOURNAMENT_ENCOUNTER_RESTORE_PENDING campaignLoaded=false";

        PlayerEncounter currentEncounter = Campaign.Current.PlayerEncounter;
        if (ReferenceEquals(currentEncounter, installedCombatFixtureEncounter))
            Campaign.Current.PlayerEncounter = originalCombatFixtureEncounter;
        else if (!ReferenceEquals(currentEncounter, originalCombatFixtureEncounter))
            return "TOURNAMENT_ENCOUNTER_RESTORE_PENDING encounterChanged=true";

        string townId = combatFixtureEncounterTownId;
        originalCombatFixtureEncounter = null;
        installedCombatFixtureEncounter = null;
        combatFixtureEncounterTownId = null;
        return $"TOURNAMENT_ENCOUNTER_RESTORED fixtureActive=false townId={townId}";
    }

    [CommandLineArgumentFunction("combat_fixture_setup", "coop.debug.tournaments")]
    public static string SetupCombatFixture(List<string> args)
    {
        if (args.Count != 2)
            return "Usage: coop.debug.tournaments.combat_fixture_setup player1-controller-id player2-controller-id";

        return DispatchCombatFixture(
            new NetworkTournamentCombatFixtureCommand(
                TournamentCombatFixtureAction.Initialize,
                args[0],
                args[1]),
            "Initialize-TournamentCombatFixture");
    }

    [CommandLineArgumentFunction("combat_fixture_ai_strike", "coop.debug.tournaments")]
    public static string InvokeAiShieldStrike(List<string> args)
    {
        if (args.Count != 1)
            return "Usage: coop.debug.tournaments.combat_fixture_ai_strike player1-controller-id";

        return DispatchCombatFixture(
            new NetworkTournamentCombatFixtureCommand(
                TournamentCombatFixtureAction.AiShieldStrike,
                args[0],
                null),
            "Invoke-AiShieldStrike");
    }

    [CommandLineArgumentFunction("combat_fixture_player_strike", "coop.debug.tournaments")]
    public static string InvokePlayerShieldStrike(List<string> args)
    {
        if (args.Count != 2)
            return "Usage: coop.debug.tournaments.combat_fixture_player_strike player1-controller-id player2-controller-id";

        return DispatchCombatFixture(
            new NetworkTournamentCombatFixtureCommand(
                TournamentCombatFixtureAction.PlayerShieldStrike,
                args[0],
                args[1]),
            "Invoke-PlayerShieldStrike");
    }

    [CommandLineArgumentFunction("combat_fixture_javelin", "coop.debug.tournaments")]
    public static string InvokeTournamentJavelinThrow(List<string> args)
    {
        if (args.Count != 1)
            return "Usage: coop.debug.tournaments.combat_fixture_javelin player1-controller-id";

        return DispatchCombatFixture(
            new NetworkTournamentCombatFixtureCommand(
                TournamentCombatFixtureAction.JavelinThrow,
                args[0],
                null),
            "Invoke-TournamentJavelinThrow");
    }

    [CommandLineArgumentFunction("combat_fixture_mounted_polearm_guard", "coop.debug.tournaments")]
    public static string InvokeMountedPolearmGuard(List<string> args)
    {
        if (args.Count != 2)
            return "Usage: coop.debug.tournaments.combat_fixture_mounted_polearm_guard player1-controller-id player2-controller-id";

        return DispatchCombatFixture(
            new NetworkTournamentCombatFixtureCommand(
                TournamentCombatFixtureAction.MountedPolearmGuard,
                args[0],
                args[1]),
            "Invoke-MountedPolearmGuard");
    }

    [CommandLineArgumentFunction("combat_fixture_mounted_polearm_strike", "coop.debug.tournaments")]
    public static string InvokeMountedPolearmStrike(List<string> args)
    {
        if (args.Count != 2)
            return "Usage: coop.debug.tournaments.combat_fixture_mounted_polearm_strike player1-controller-id player2-controller-id";

        return DispatchCombatFixture(
            new NetworkTournamentCombatFixtureCommand(
                TournamentCombatFixtureAction.MountedPolearmStrike,
                args[0],
                args[1]),
            "Invoke-MountedPolearmStrike");
    }

    [CommandLineArgumentFunction("combat_fixture_restore", "coop.debug.tournaments")]
    public static string RestoreCombatFixture(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.tournaments.combat_fixture_restore";

        return DispatchCombatFixture(
            new NetworkTournamentCombatFixtureCommand(
                TournamentCombatFixtureAction.Restore,
                null,
                null),
            "Restore-TournamentCombatFixture");
    }

    private static string DispatchCombatFixture(
        NetworkTournamentCombatFixtureCommand command,
        string anchor)
    {
        if (ModInformation.IsClient)
            return "This function can only be used by the server";
        if (!ContainerProvider.TryResolve<INetwork>(out var network))
            return "Unable to resolve the campaign network";

        network.SendAll(command);
        return $"{anchor}: command sent to tournament clients";
    }

    private static bool TryResolveFixtureParticipant(
        string controllerId,
        IPlayerManager playerManager,
        IObjectManager objectManager,
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        out TournamentCampaignParticipant participant,
        out string error)
    {
        participant = null;
        error = null;
        if (!playerManager.TryGetPlayer(controllerId, out var player))
        {
            error = $"No registered player has controller id {controllerId}";
            return false;
        }
        if (!playerManager.IsConnected(player))
        {
            error = $"Player {controllerId} is not connected";
            return false;
        }
        if (!objectManager.TryGetObjectWithLogging(player.MobilePartyId, out MobileParty party))
        {
            error = $"Unable to resolve player party {player.MobilePartyId}";
            return false;
        }
        if (party.CurrentSettlement != null)
        {
            error = $"Player {controllerId} is already in {party.CurrentSettlement.Name}";
            return false;
        }
        if (party.MapEvent != null)
        {
            error = $"Player {controllerId} is already in a map event";
            return false;
        }
        if (party.IsCurrentlyAtSea)
        {
            error = $"Player {controllerId} must be on land before preparing the tournament fixture";
            return false;
        }
        if (!behaviorSnapshot.TryCreate(party, out PartyBehaviorUpdateData behavior) ||
            !behaviorSnapshot.CanApply(party, behavior))
        {
            error = $"Unable to capture player party behavior for {controllerId}";
            return false;
        }

        participant = new TournamentCampaignParticipant(controllerId, party, behavior);
        return true;
    }

    private static bool RestoreParticipant(
        TournamentCampaignParticipant participant,
        IMobilePartyBehaviorSnapshot behaviorSnapshot)
    {
        if (participant?.Party == null || behaviorSnapshot == null)
            return false;

        if (participant.Party.CurrentSettlement != null)
            LeaveSettlementAction.ApplyForParty(participant.Party);
        participant.Party.Position = participant.OriginalBehavior.PartyPosition;
        participant.Party.IsCurrentlyAtSea = participant.OriginalBehavior.IsCurrentlyAtSea;
        if (!behaviorSnapshot.TryApply(
                participant.Party,
                participant.OriginalBehavior,
                out _))
            return false;

        MessageBroker.Instance.Publish(
            typeof(TournamentDebugCommand),
            new PartyBehaviorChangeAttempted(
                participant.Party,
                forcePosition: true,
                isCurrentlyAtSea: participant.OriginalBehavior.IsCurrentlyAtSea,
                resetMovementToHold: false));
        return true;
    }

    private static TournamentMatchData GetCurrentMatch(TournamentSessionSnapshot snapshot)
        => snapshot?.Rounds?
            .SelectMany(round => round.Matches)
            .FirstOrDefault(match => match.MatchId == snapshot.CurrentMatchId);

    private static string FormatPosition(CampaignVec2 position)
        => $"{position.X:R},{position.Y:R},{position.IsOnLand}";

    private sealed class TournamentCampaignParticipant
    {
        public string ControllerId { get; }
        public MobileParty Party { get; }
        public PartyBehaviorUpdateData OriginalBehavior { get; }
        public CampaignVec2 OriginalPosition => OriginalBehavior.PartyPosition;

        public TournamentCampaignParticipant(
            string controllerId,
            MobileParty party,
            PartyBehaviorUpdateData originalBehavior)
        {
            ControllerId = controllerId;
            Party = party;
            OriginalBehavior = originalBehavior;
        }
    }

    private static string PrepareCombatFixtureEncounter(string townId, Town town)
    {
        if (Campaign.Current == null)
            return "No campaign is currently loaded";
        if (installedCombatFixtureEncounter != null)
        {
            if (!ReferenceEquals(Campaign.Current.PlayerEncounter, installedCombatFixtureEncounter) ||
                combatFixtureEncounterTownId != townId)
            {
                return "A different tournament encounter fixture is already active";
            }
            return null;
        }
        if (Campaign.Current.PlayerEncounter != null)
        {
            if (PlayerEncounter.EncounterSettlement == town.Settlement)
                return null;
            return "A different player encounter is already active";
        }

        originalCombatFixtureEncounter = Campaign.Current.PlayerEncounter;
        try
        {
            PlayerEncounter.Start();
            installedCombatFixtureEncounter = PlayerEncounter.Current;
            installedCombatFixtureEncounter.EncounterSettlementAux = town.Settlement;
            combatFixtureEncounterTownId = townId;
            return null;
        }
        catch (Exception exception)
        {
            Campaign.Current.PlayerEncounter = originalCombatFixtureEncounter;
            originalCombatFixtureEncounter = null;
            installedCombatFixtureEncounter = null;
            combatFixtureEncounterTownId = null;
            return $"Unable to prepare the local tournament encounter: {exception.Message}";
        }
    }

    private sealed class TournamentCampaignFixture
    {
        public Town Town { get; }
        public string TownId { get; }
        public bool TournamentAdded { get; }
        public TournamentCampaignParticipant PlayerOne { get; }
        public TournamentCampaignParticipant PlayerTwo { get; }

        public TournamentCampaignFixture(
            Town town,
            string townId,
            bool tournamentAdded,
            TournamentCampaignParticipant playerOne,
            TournamentCampaignParticipant playerTwo)
        {
            Town = town;
            TownId = townId;
            TournamentAdded = tournamentAdded;
            PlayerOne = playerOne;
            PlayerTwo = playerTwo;
        }
    }
#endif
}
