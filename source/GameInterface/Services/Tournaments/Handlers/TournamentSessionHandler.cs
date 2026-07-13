using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Tournaments.Handlers;

internal sealed partial class TournamentSessionHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TournamentSessionHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly ITournamentSessionRegistry sessionRegistry;
    private readonly ITournamentGameInterface tournamentGameInterface;
    private readonly ITournamentNativeRemovalAuthorization nativeRemovalAuthorization;
    private readonly ITournamentSaveDeferral saveDeferral;
    private readonly Dictionary<string, BetLedgerEntry> betLedger = new();
    private readonly Dictionary<string, TournamentCompletionTransaction> completionTransactions = new();
    private readonly HashSet<string> completionInProgress = new();
    private readonly HashSet<string> liveCombatSessions = new();
    private readonly HashSet<string> acceptedHitProgression = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<NetPeer, string> tournamentPeerControllers = new();

    public TournamentSessionHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IControllerIdProvider controllerIdProvider,
        ITournamentSessionRegistry sessionRegistry,
        ITournamentGameInterface tournamentGameInterface,
        ITournamentNativeRemovalAuthorization nativeRemovalAuthorization,
        ITournamentSaveDeferral saveDeferral)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.controllerIdProvider = controllerIdProvider;
        this.sessionRegistry = sessionRegistry;
        this.tournamentGameInterface = tournamentGameInterface;
        this.nativeRemovalAuthorization = nativeRemovalAuthorization;
        this.saveDeferral = saveDeferral;

        messageBroker.Subscribe<NetworkRequestJoinTournament>(Handle_Join);
        messageBroker.Subscribe<NetworkRequestLeaveTournamentPreparation>(Handle_LeavePreparation);
        messageBroker.Subscribe<NetworkRequestStartTournament>(Handle_Start);
        messageBroker.Subscribe<NetworkRequestSpectateTournament>(Handle_Spectate);
        messageBroker.Subscribe<NetworkRequestLeaveActiveTournament>(Handle_LeaveActive);
        messageBroker.Subscribe<NetworkTournamentMissionEntered>(Handle_TournamentMissionEntered);
        messageBroker.Subscribe<NetworkRequestTournamentChoice>(Handle_Choice);
        messageBroker.Subscribe<NetworkRequestTournamentBet>(Handle_Bet);
        messageBroker.Subscribe<NetworkSubmitTournamentSpawnManifest>(Handle_SpawnManifest);
        messageBroker.Subscribe<NetworkSubmitTournamentMatchResult>(Handle_MatchResult);
        messageBroker.Subscribe<NetworkSubmitTournamentHitProgression>(Handle_HitProgression);
        messageBroker.Subscribe<NetworkTournamentSessionSnapshot>(Handle_Snapshot);
        messageBroker.Subscribe<NetworkTournamentSpawnManifest>(Handle_SpawnManifestSnapshot);
        messageBroker.Subscribe<NetworkEnterTournamentMission>(Handle_EnterMission);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_Disconnected);
        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestJoinTournament>(Handle_Join);
        messageBroker.Unsubscribe<NetworkRequestLeaveTournamentPreparation>(Handle_LeavePreparation);
        messageBroker.Unsubscribe<NetworkRequestStartTournament>(Handle_Start);
        messageBroker.Unsubscribe<NetworkRequestSpectateTournament>(Handle_Spectate);
        messageBroker.Unsubscribe<NetworkRequestLeaveActiveTournament>(Handle_LeaveActive);
        messageBroker.Unsubscribe<NetworkTournamentMissionEntered>(Handle_TournamentMissionEntered);
        messageBroker.Unsubscribe<NetworkRequestTournamentChoice>(Handle_Choice);
        messageBroker.Unsubscribe<NetworkRequestTournamentBet>(Handle_Bet);
        messageBroker.Unsubscribe<NetworkSubmitTournamentSpawnManifest>(Handle_SpawnManifest);
        messageBroker.Unsubscribe<NetworkSubmitTournamentMatchResult>(Handle_MatchResult);
        messageBroker.Unsubscribe<NetworkSubmitTournamentHitProgression>(Handle_HitProgression);
        messageBroker.Unsubscribe<NetworkTournamentSessionSnapshot>(Handle_Snapshot);
        messageBroker.Unsubscribe<NetworkTournamentSpawnManifest>(Handle_SpawnManifestSnapshot);
        messageBroker.Unsubscribe<NetworkEnterTournamentMission>(Handle_EnterMission);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_Disconnected);
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
    }

    private void Handle_Join(MessagePayload<NetworkRequestJoinTournament> payload)
    {
        if (ModInformation.IsClient || !TryAuthenticate(payload.Who, out var peer, out var player))
            return;

        GameThread.RunSafe(
            () => ProcessJoin(peer, player, payload.What),
            context: nameof(Handle_Join));
    }

    private void ProcessJoin(
        NetPeer peer,
        Player player,
        NetworkRequestJoinTournament request)
    {
        if (HasEnrollmentInAnotherTown(player.ControllerId, request.TownId))
        {
            SendRejection(peer, request.TownId, "You are already enrolled in a tournament in another town.");
            return;
        }
        if (!TryResolvePlayerAtTown(player, request.TownId, out var town, out var hero, out _))
        {
            SendRejection(peer, request.TownId, "Your registered party must be in this town to enter its tournament.");
            return;
        }
        if (!TryResolveJoinSession(peer, request.TownId, town, out var current))
            return;
        if (!string.IsNullOrEmpty(request.SessionId) && request.SessionId != current.SessionId)
        {
            SendCanonical(peer, current);
            return;
        }
        if (current.Phase != TournamentSessionPhase.Preparation)
        {
            RequestActiveSpectate(peer, player.ControllerId, current);
            return;
        }

        JoinPreparation(peer, player, hero, request, current);
    }

    private bool HasEnrollmentInAnotherTown(string controllerId, string townId)
    {
        return sessionRegistry.GetAll().Any(session =>
            session.TownId != townId &&
            session.Contestants.Any(contestant =>
                contestant.IsHuman && contestant.ControllerId == controllerId));
    }

    private bool TryResolveJoinSession(
        NetPeer peer,
        string townId,
        Town town,
        out TournamentSessionSnapshot current)
    {
        bool hasSession = sessionRegistry.TryGetByTown(townId, out current);
        if (hasSession && current.Phase != TournamentSessionPhase.Preparation)
            return true;

        TournamentGame nativeGame = Campaign.Current?.TournamentManager?.GetTournamentGame(town);
        if (nativeGame == null)
        {
            SendRejection(peer, townId, "There is no active tournament in this town.");
            return false;
        }
        if (nativeGame.GetType() != typeof(FightTournamentGame))
        {
            SendRejection(peer, townId,
                "Cooperative tournaments support only the standard Fight Tournament in Bannerlord v1.4.7.");
            return false;
        }
        if (hasSession)
            return true;

        if (!tournamentGameInterface.TryFreezeTournament(town, (FightTournamentGame)nativeGame, out var seed) ||
            sessionRegistry.TryCreate(seed, out current) == TournamentMutationStatus.Rejected)
        {
            SendRejection(peer, townId, "The tournament could not be prepared for cooperative play.");
            return false;
        }
        return true;
    }

    private void RequestActiveSpectate(
        NetPeer peer,
        string controllerId,
        TournamentSessionSnapshot current)
    {
        TournamentMutationStatus status = sessionRegistry.TryRequestSpectate(
            current.SessionId,
            current.Revision,
            controllerId,
            out current);
        LogSpectatorRequest(controllerId, status, current);
        if (status != TournamentMutationStatus.Applied)
            return;

        BroadcastSnapshot(current);
        network.Send(peer, new NetworkEnterTournamentMission(current, true));
    }

    private void JoinPreparation(
        NetPeer peer,
        Player player,
        Hero hero,
        NetworkRequestJoinTournament request,
        TournamentSessionSnapshot current)
    {
        long expectedRevision = string.IsNullOrEmpty(request.SessionId)
            ? current.Revision
            : request.ExpectedRevision;
        TournamentMutationStatus status = sessionRegistry.TryJoin(
            current.SessionId,
            expectedRevision,
            player.ControllerId,
            player.CharacterObjectId,
            hero.Name?.ToString() ?? player.ControllerId,
            MBRandom.RandomInt(int.MaxValue),
            hero.IsLord,
            out var snapshot);
        if (status == TournamentMutationStatus.Full)
        {
            SendRejection(peer, request.TownId, "This tournament already has 16 human competitors.");
            SendCanonical(peer, snapshot);
            return;
        }
        PublishMutation(status, peer, snapshot);
    }
    private void Handle_LeavePreparation(MessagePayload<NetworkRequestLeaveTournamentPreparation> payload)
    {
        if (ModInformation.IsClient || !TryAuthenticate(payload.Who, out var peer, out var player))
            return;

        GameThread.RunSafe(() =>
        {
            if (!sessionRegistry.TryGet(payload.What.SessionId, out var current))
                return;

            var status = sessionRegistry.TryLeavePreparation(
                payload.What.SessionId,
                payload.What.ExpectedRevision,
                player.ControllerId,
                out var snapshot,
                out var removed);
            if (removed)
            {
                var removal = new NetworkTournamentSessionRemoved(current.SessionId, current.TownId);
                network.SendAll(removal);
                messageBroker.Publish(this, new TournamentSessionRemoved(current.SessionId, current.TownId));
                return;
            }
            PublishMutation(status, peer, snapshot);
        }, context: nameof(Handle_LeavePreparation));
    }

    private void Handle_Start(MessagePayload<NetworkRequestStartTournament> payload)
    {
        if (ModInformation.IsClient || !TryAuthenticate(payload.Who, out var peer, out var player))
            return;

        GameThread.RunSafe(() =>
        {
            if (!sessionRegistry.TryGet(payload.What.SessionId, out var current) ||
                current.Revision != payload.What.ExpectedRevision ||
                current.Phase != TournamentSessionPhase.Preparation ||
                !TryResolvePlayerAtTown(player, current.TownId, out _, out _, out _) ||
                !current.Contestants.Any(contestant =>
                    contestant.IsHuman && contestant.ControllerId == player.ControllerId))
            {
                SendCanonical(peer, current);
                return;
            }

            if (Campaign.Current?.SaveHandler?.IsSaving == true)
            {
                SendRejection(peer, current.TownId, "Wait for the current campaign save to finish before starting the tournament.");
                return;
            }

            if (!tournamentGameInterface.TryApplyLockedPrize(current))
            {
                Logger.Error("Could not apply locked prize for tournament session {SessionId}", current.SessionId);
                SendRejection(peer, current.TownId, "The locked tournament prize is no longer available.");
                return;
            }
            messageBroker.Publish(this, new TournamentNativeStateChanged());

            if (!tournamentGameInterface.TryCreateBracket(current, out var bracket))
                return;

            var status = sessionRegistry.TryStart(
                payload.What.SessionId,
                payload.What.ExpectedRevision,
                player.ControllerId,
                bracket.Rounds,
                bracket.CurrentMatchId,
                out var snapshot);
            PublishMutation(status, peer, snapshot);
            if (status == TournamentMutationStatus.Applied)
            {
                Logger.Information(
                    "[Tournament] Started session {SessionId} in town {TownId}: humans={HumanCount}, spectators={SpectatorCount}, voters={VoterCount}, match={MatchId}, revision={Revision}, startedBy={ControllerId}",
                    snapshot.SessionId,
                    snapshot.TownId,
                    CountActiveHumans(snapshot),
                    snapshot.SpectatorControllerIds.Length,
                    snapshot.VoterCount,
                    snapshot.CurrentMatchId,
                    snapshot.Revision,
                    player.ControllerId);
                network.SendAll(new NetworkEnterTournamentMission(snapshot, false));
            }
        }, context: nameof(Handle_Start));
    }

    private void Handle_Spectate(MessagePayload<NetworkRequestSpectateTournament> payload)
    {
        if (ModInformation.IsClient || !TryAuthenticate(payload.Who, out var peer, out var player))
            return;

        GameThread.RunSafe(() =>
        {
            if (!sessionRegistry.TryGet(payload.What.SessionId, out var current) ||
                !TryResolvePlayerAtTown(player, current.TownId, out _, out _, out _))
            {
                return;
            }

            var status = sessionRegistry.TryRequestSpectate(
                payload.What.SessionId,
                payload.What.ExpectedRevision,
                player.ControllerId,
                out var snapshot);
            LogSpectatorRequest(player.ControllerId, status, snapshot);
            if (status == TournamentMutationStatus.Applied)
            {
                BroadcastSnapshot(snapshot);
                network.Send(peer, new NetworkEnterTournamentMission(snapshot, true));
            }
            else
                SendCanonical(peer, snapshot);
        }, context: nameof(Handle_Spectate));
    }

    private void Handle_TournamentMissionEntered(MessagePayload<NetworkTournamentMissionEntered> payload)
    {
        if (ModInformation.IsClient || !TryAuthenticate(payload.Who, out var peer, out var player))
            return;

        GameThread.RunSafe(() =>
        {
            if (!sessionRegistry.TryGet(payload.What.SessionId, out var current) ||
                current.Phase == TournamentSessionPhase.Preparation ||
                current.IsCompleted)
                return;

            var status = sessionRegistry.TryEnterMission(
                current.SessionId,
                payload.What.ExpectedRevision,
                player.ControllerId,
                out var snapshot);
            TournamentSessionSnapshot canonical = snapshot ?? current;
            Logger.Information(
                "[Tournament] Mission entry session={SessionId}, controller={ControllerId}, role={Role}, status={Status}, entrants={EntrantCount}, humans={HumanCount}, spectators={SpectatorCount}, expectedRevision={ExpectedRevision}, revision={Revision}",
                current.SessionId,
                player.ControllerId,
                GetPlayerRole(canonical, player.ControllerId),
                status,
                CountEntrants(canonical),
                CountActiveHumans(canonical),
                canonical.SpectatorControllerIds.Length,
                payload.What.ExpectedRevision,
                canonical.Revision);
            PublishMutation(status, peer, snapshot);
            if (status == TournamentMutationStatus.Applied &&
                sessionRegistry.TryGetSpawnManifest(current.SessionId, out var manifest))
                network.Send(peer, new NetworkTournamentSpawnManifest(manifest));
        }, context: nameof(Handle_TournamentMissionEntered));
    }

    private void Handle_Choice(MessagePayload<NetworkRequestTournamentChoice> payload)
    {
        if (ModInformation.IsClient || !TryAuthenticate(payload.Who, out var peer, out var player))
            return;

        GameThread.RunSafe(() =>
        {
            if (!sessionRegistry.TryGet(payload.What.SessionId, out var current))
            {
                SendCanonical(peer, current);
                return;
            }

            var status = sessionRegistry.TryChoose(
                payload.What.SessionId,
                payload.What.ExpectedRevision,
                payload.What.MatchId,
                player.ControllerId,
                payload.What.Choice,
                out var snapshot,
                out var outcome);
            TournamentSessionSnapshot canonical = snapshot ?? current;
            Logger.Information(
                "[Tournament] Vote session={SessionId}, match={MatchId}, controller={ControllerId}, role={Role}, choice={Choice}, status={Status}, ready={ReadyCount}/{ReadyVoterCount}, skip={SkipCount}/{SkipVoterCount}, outcome={Outcome}, expectedRevision={ExpectedRevision}, revision={Revision}",
                current.SessionId,
                payload.What.MatchId,
                player.ControllerId,
                GetPlayerRole(canonical, player.ControllerId),
                payload.What.Choice,
                status,
                canonical.ReadyCount,
                canonical.VoterCount,
                canonical.SkipCount,
                canonical.VoterCount,
                outcome,
                payload.What.ExpectedRevision,
                canonical.Revision);
            if (status == TournamentMutationStatus.Applied && outcome == TournamentBallotOutcome.SimulateMatch)
            {
                TournamentSessionSnapshot simulated = SimulateCurrentMatchAndAdvance(snapshot, player.ControllerId);
                if (simulated.Revision == snapshot.Revision)
                    BroadcastSnapshot(snapshot);
            }
            else
            {
                PublishMutation(status, peer, snapshot);
            }
        }, context: nameof(Handle_Choice));
    }

    private void Handle_LeaveActive(MessagePayload<NetworkRequestLeaveActiveTournament> payload)
    {
        if (ModInformation.IsClient || !TryAuthenticate(payload.Who, out var peer, out var player))
            return;

        GameThread.RunSafe(() => LeaveActive(
            payload.What.SessionId,
            payload.What.ExpectedRevision,
            player.ControllerId,
            peer), context: nameof(Handle_LeaveActive));
    }

    private void Handle_Bet(MessagePayload<NetworkRequestTournamentBet> payload)
    {
        if (ModInformation.IsClient || !TryAuthenticate(payload.Who, out var peer, out var player))
            return;

        GameThread.RunSafe(
            () => ProcessBetRequest(peer, player, payload.What),
            context: nameof(Handle_Bet));
    }

    private void Handle_SpawnManifest(MessagePayload<NetworkSubmitTournamentSpawnManifest> payload)
    {
        if (ModInformation.IsClient || !TryAuthenticate(payload.Who, out var peer, out var player))
            return;

        GameThread.RunSafe(() =>
        {
            TournamentSpawnManifestData manifest = payload.What.Manifest;
            TournamentSessionSnapshot current = null;
            bool validManifest = manifest != null &&
                sessionRegistry.TryGet(manifest.SessionId, out current) &&
                current.Phase == TournamentSessionPhase.LiveMatch &&
                current.HostControllerId == player.ControllerId &&
                manifest.Revision == current.Revision &&
                manifest.BracketRevision == current.BracketRevision &&
                manifest.MatchId == current.CurrentMatchId &&
                TournamentSpawnManifestValidator.IsValid(manifest, current);
            bool validObjects = validManifest && HasValidManifestObjects(manifest);
            if (!validManifest || !validObjects)
            {
                Logger.Warning(
                    "[Tournament] Rejected spawn manifest session={SessionId}, controller={ControllerId}, " +
                    "phase={Phase}, revision={ManifestRevision}/{CurrentRevision}, " +
                    "bracket={ManifestBracket}/{CurrentBracket}, match={ManifestMatch}/{CurrentMatch}, " +
                    "structure={ValidManifest}, objects={ValidObjects}",
                    manifest?.SessionId,
                    player.ControllerId,
                    current?.Phase,
                    manifest?.Revision,
                    current?.Revision,
                    manifest?.BracketRevision,
                    current?.BracketRevision,
                    manifest?.MatchId,
                    current?.CurrentMatchId,
                    validManifest,
                    validObjects);
                SendCanonical(peer, current);
                return;
            }

            var status = sessionRegistry.TryStoreSpawnManifest(manifest, player.ControllerId, out var snapshot);
            if (status == TournamentMutationStatus.Applied)
            {
                var message = new NetworkTournamentSpawnManifest(payload.What.Manifest);
                network.SendAll(message);
                messageBroker.Publish(this, new TournamentSpawnManifestUpdated(payload.What.Manifest));
            }
            else
            {
                SendCanonical(peer, snapshot);
            }
        }, context: nameof(Handle_SpawnManifest));
    }

    private void Handle_MatchResult(MessagePayload<NetworkSubmitTournamentMatchResult> payload)
    {
        if (ModInformation.IsClient || !TryAuthenticate(payload.Who, out var peer, out var player))
            return;

        GameThread.RunSafe(() =>
        {
            TournamentMatchResultData result = payload.What.Result;
            TournamentSessionSnapshot current = null;
            TournamentSpawnManifestData manifest = null;
            if (result == null ||
                !sessionRegistry.TryGet(result.SessionId, out current) ||
                current.Phase != TournamentSessionPhase.LiveMatch ||
                current.HostControllerId != player.ControllerId ||
                !sessionRegistry.TryGetSpawnManifest(result.SessionId, out manifest) ||
                manifest.MatchId != current.CurrentMatchId ||
                manifest.BracketRevision != current.BracketRevision ||
                manifest.Revision > current.Revision ||
                result.WinnerTeamIds == null ||
                result.WinnerSlotIds == null ||
                result.TeamScores == null ||
                result.TeamScores.Any(score => score == null) ||
                result.Revision != current.Revision ||
                result.BracketRevision != current.BracketRevision ||
                result.MatchId != current.CurrentMatchId)
            {
                Logger.Warning(
                    "[Tournament] Rejected live match result before bracket advance session={SessionId}, match={ResultMatch}/{CurrentMatch}, controller={ControllerId}, revision={ResultRevision}/{CurrentRevision}, bracket={ResultBracket}/{CurrentBracket}, manifest={ManifestMatch}",
                    result?.SessionId,
                    result?.MatchId,
                    current?.CurrentMatchId,
                    player.ControllerId,
                    result?.Revision,
                    current?.Revision,
                    result?.BracketRevision,
                    current?.BracketRevision,
                    manifest?.MatchId);
                SendCanonical(peer, current);
                return;
            }

            if (!tournamentGameInterface.TryAdvanceBracket(current, result, out var bracket))
            {
                Logger.Warning(
                    "[Tournament] Failed to advance live bracket session={SessionId}, match={MatchId}, winners={WinnerSlots}, scores={Scores}",
                    result.SessionId,
                    result.MatchId,
                    string.Join(",", result.WinnerSlotIds),
                    string.Join(",", result.TeamScores.Select(score => $"{score.TeamId}:{score.Score}")));
                SendCanonical(peer, current);
                return;
            }

            IReadOnlyDictionary<string, int> contestantScores =
                TournamentStateReconciliation.ReconcileContestantScores(
                    current,
                    bracket.ContestantScores,
                    out bool scoresChanged);
            Logger.Information(
                "[Tournament] Committing live result session={SessionId}, match={MatchId}: sequence={Sequence}, candidateScores={CandidateScoreCount}, canonicalScores={CanonicalScoreCount}, corrected={ScoresChanged}",
                result.SessionId,
                result.MatchId,
                result.Sequence,
                bracket.ContestantScores.Count,
                contestantScores.Count,
                scoresChanged);
            var status = sessionRegistry.TryApplyMatchResult(
                result,
                player.ControllerId,
                bracket.Rounds,
                contestantScores,
                bracket.CurrentMatchId,
                bracket.WinnerSlotId,
                bracket.IsCompleted,
                out var snapshot);
            if (status != TournamentMutationStatus.Applied)
            {
                Logger.Warning(
                    "[Tournament] Match result registry rejected session={SessionId}, match={MatchId}, status={Status}, sequence={Sequence}",
                    result.SessionId,
                    result.MatchId,
                    status,
                    result.Sequence);
                SendCanonical(peer, snapshot);
                return;
            }

            liveCombatSessions.Add(result.SessionId);
            ResolveBets(current, bracket.MatchWinnerSlotIds);
            BroadcastSnapshot(snapshot);
            if (bracket.IsCompleted)
                CompleteTournament(snapshot);
        }, context: nameof(Handle_MatchResult));
    }

    private void Handle_Snapshot(MessagePayload<NetworkTournamentSessionSnapshot> payload)
    {
        if (ModInformation.IsServer ||
            !TournamentServerMessageGuard.IsTrusted(payload.Who))
            return;

        GameThread.RunSafe(() =>
        {
            TournamentSessionSnapshot snapshot = TournamentSessionSnapshotNormalizer.Normalize(payload.What.Snapshot);
            if (sessionRegistry.ApplySnapshot(snapshot))
                messageBroker.Publish(this, new TournamentSessionUpdated(snapshot));
        }, context: nameof(Handle_Snapshot));
    }

    private void Handle_SpawnManifestSnapshot(MessagePayload<NetworkTournamentSpawnManifest> payload)
    {
        if (ModInformation.IsServer ||
            !TournamentServerMessageGuard.IsTrusted(payload.Who))
            return;

        GameThread.RunSafe(
            () => messageBroker.Publish(this, new TournamentSpawnManifestUpdated(payload.What.Manifest)),
            context: nameof(Handle_SpawnManifestSnapshot));
    }

    private void Handle_EnterMission(MessagePayload<NetworkEnterTournamentMission> payload)
    {
        if (ModInformation.IsServer ||
            !TournamentServerMessageGuard.IsTrusted(payload.Who))
            return;

        GameThread.RunSafe(() =>
        {
            TournamentSessionSnapshot snapshot = TournamentSessionSnapshotNormalizer.Normalize(payload.What.Snapshot);
            if (snapshot == null)
                return;
            if (!payload.What.IsSpectator && !snapshot.Contestants.Any(contestant =>
                    contestant.IsHuman && contestant.ControllerId == controllerIdProvider.ControllerId))
            {
                return;
            }

            if (!ContainerProvider.TryResolve(out ICoopTournamentLauncher launcher))
            {
                Logger.Error("Could not resolve {Launcher}", nameof(ICoopTournamentLauncher));
                return;
            }

            if (launcher.OpenCoopTournament(snapshot, payload.What.IsSpectator) == null)
                return;
        }, context: nameof(Handle_EnterMission));
    }

    private void Handle_Disconnected(MessagePayload<PlayerDisconnected> payload)
    {
        if (ModInformation.IsClient)
            return;
        if (!TryResolveDisconnectedController(payload.What.PlayerId, out var controllerId))
            return;

        GameThread.RunSafe(
            () => ProcessDisconnected(controllerId),
            context: nameof(Handle_Disconnected));
    }

    private bool TryResolveDisconnectedController(NetPeer playerId, out string controllerId)
    {
        if (playerManager.TryGetPlayer(playerId, out var player))
            controllerId = player.ControllerId;
        else if (!tournamentPeerControllers.TryGetValue(playerId, out controllerId))
            return false;

        tournamentPeerControllers.TryRemove(playerId, out _);
        return true;
    }

    private void ProcessDisconnected(string controllerId)
    {
        foreach (TournamentSessionSnapshot snapshot in sessionRegistry.GetAll())
            ProcessDisconnectedSession(controllerId, snapshot);
    }

    private void ProcessDisconnectedSession(
        string controllerId,
        TournamentSessionSnapshot snapshot)
    {
        bool involved = snapshot.Contestants.Any(contestant => contestant.ControllerId == controllerId) ||
            snapshot.SpectatorControllerIds.Contains(controllerId);
        if (!involved && snapshot.Phase == TournamentSessionPhase.Preparation)
            return;

        if (snapshot.Phase == TournamentSessionPhase.Preparation)
        {
            LeavePreparationAfterDisconnect(controllerId, snapshot);
            return;
        }
        if (involved)
            LeaveActive(snapshot.SessionId, snapshot.Revision, controllerId, null);
    }

    private void LeavePreparationAfterDisconnect(
        string controllerId,
        TournamentSessionSnapshot snapshot)
    {
        TournamentMutationStatus status = sessionRegistry.TryLeavePreparation(
            snapshot.SessionId,
            snapshot.Revision,
            controllerId,
            out var changed,
            out var removed);
        if (status != TournamentMutationStatus.Applied)
            return;
        if (removed)
        {
            network.SendAll(new NetworkTournamentSessionRemoved(snapshot.SessionId, snapshot.TownId));
            messageBroker.Publish(this, new TournamentSessionRemoved(snapshot.SessionId, snapshot.TownId));
        }
        else if (changed != null)
        {
            BroadcastSnapshot(changed);
        }
    }

    private void LeaveActive(
        string sessionId,
        long expectedRevision,
        string controllerId,
        NetPeer peer)
    {
        if (!sessionRegistry.TryGet(sessionId, out var current))
        {
            SendCanonical(peer, current);
            return;
        }

        bool isMember = IsActiveMember(current, controllerId);
        if (TryHandleCompletedLeave(current, controllerId, isMember, peer))
            return;
        if (!TryResolveReplacementName(current, out var replacementName))
        {
            SendCanonical(peer, current);
            return;
        }
        if (expectedRevision != current.Revision && !isMember)
        {
            SendCanonical(peer, current);
            return;
        }

        ApplyActiveLeave(current, controllerId, replacementName, peer);
    }

    private static bool IsActiveMember(
        TournamentSessionSnapshot snapshot,
        string controllerId)
    {
        return snapshot.Contestants.Any(contestant =>
                contestant.IsHuman && contestant.ControllerId == controllerId) ||
            snapshot.SpectatorControllerIds.Contains(controllerId);
    }

    private bool TryHandleCompletedLeave(
        TournamentSessionSnapshot current,
        string controllerId,
        bool isMember,
        NetPeer peer)
    {
        if (!current.IsCompleted)
            return false;
        if (!isMember)
        {
            SendCanonical(peer, current);
            return true;
        }

        Logger.Information(
            "[Tournament] Completed tournament leave requested session={SessionId}, controller={ControllerId}",
            current.SessionId,
            controllerId);
        FinalizeCompletedTournament(current);
        return true;
    }

    private bool TryResolveReplacementName(
        TournamentSessionSnapshot current,
        out string replacementName)
    {
        replacementName = null;
        if (!objectManager.TryGetObject(current.TownId, out Town town) ||
            town.Culture?.BasicTroop == null ||
            !objectManager.TryGetId(town.Culture.BasicTroop, out _))
            return false;

        replacementName = town.Culture.BasicTroop.Name?.ToString() ?? "Tournament Recruit";
        return true;
    }

    private void ApplyActiveLeave(
        TournamentSessionSnapshot current,
        string controllerId,
        string replacementName,
        NetPeer peer)
    {
        TournamentSpawnManifestData migrationManifest = null;
        if (current.HostControllerId == controllerId)
            sessionRegistry.TryGetSpawnManifest(current.SessionId, out migrationManifest);

        TournamentMutationStatus status = sessionRegistry.TryLeaveActive(
            current.SessionId,
            current.Revision,
            controllerId,
            MBRandom.RandomInt(int.MaxValue),
            replacementName,
            out var snapshot,
            out var outcome,
            out var noViewers);
        PublishHostMigration(current, snapshot, status, migrationManifest);
        if (status == TournamentMutationStatus.Applied)
            SettleBetLedger(current.SessionId, controllerId, snapshot.Revision, current.CurrentMatchId, "Tournament bet forfeited", peer);
        PublishMutation(status, peer, snapshot);
        if (status == TournamentMutationStatus.Applied && outcome == TournamentBallotOutcome.SimulateMatch)
            snapshot = SimulateCurrentMatchAndAdvance(snapshot, controllerId);
        if (status == TournamentMutationStatus.Applied && noViewers && snapshot != null)
            ResolveAfterLastHumanLeaves(snapshot);
    }

    private void PublishHostMigration(
        TournamentSessionSnapshot previous,
        TournamentSessionSnapshot snapshot,
        TournamentMutationStatus status,
        TournamentSpawnManifestData migrationManifest)
    {
        if (status != TournamentMutationStatus.Applied ||
            migrationManifest == null ||
            snapshot.HostControllerId == previous.HostControllerId)
            return;

        network.SendAll(new NetworkTournamentSpawnManifest(migrationManifest));
        messageBroker.Publish(this, new TournamentSpawnManifestUpdated(migrationManifest));
    }

    private void ResolveAfterLastHumanLeaves(TournamentSessionSnapshot snapshot)
    {
        Logger.Information(
            "[Tournament] Last human left active tournament session={SessionId}, phase={Phase}, revision={Revision}; resolving remaining bracket immediately",
            snapshot.SessionId,
            snapshot.Phase,
            snapshot.Revision);
        if (!snapshot.IsCompleted)
            SimulateRemainingTournament(snapshot);

        if (sessionRegistry.TryGet(snapshot.SessionId, out var completed) && completed.IsCompleted)
        {
            Logger.Information(
                "[Tournament] Last-human leave completed simulation session={SessionId}, winner={WinnerSlotId}; finalizing immediately",
                completed.SessionId,
                completed.WinnerSlotId);
            FinalizeCompletedTournament(completed);
            return;
        }

        Logger.Error(
            "[Tournament] Last-human leave failed to complete tournament session={SessionId}; canonicalPresent={CanonicalPresent}, phase={Phase}, match={MatchId}, revision={Revision}",
            snapshot.SessionId,
            completed != null,
            completed?.Phase,
            completed?.CurrentMatchId,
            completed?.Revision);
    }
    private bool TryAuthenticate(object source, out NetPeer peer, out Player player)
    {
        peer = source as NetPeer;
        player = null;
        if (peer == null || !playerManager.TryGetPlayer(peer, out player))
        {
            Logger.Warning("Rejected tournament request without an authenticated player peer");
            return false;
        }
        tournamentPeerControllers[peer] = player.ControllerId;
        return true;
    }

    private bool TryResolvePlayerAtTown(
        Player player,
        string townId,
        out Town town,
        out Hero hero,
        out MobileParty party)
    {
        town = null;
        hero = null;
        party = null;
        if (!objectManager.TryGetObject(townId, out town))
            return false;
        if (!TryResolvePlayer(player, out hero, out party))
            return false;
        return party.CurrentSettlement == town.Settlement && hero.PartyBelongedTo == party;
    }

    private bool TryResolvePlayer(Player player, out Hero hero, out MobileParty party)
    {
        hero = null;
        party = null;
        if (player == null || !objectManager.TryGetObject(player.HeroId, out hero))
            return false;
        if (!objectManager.TryGetObject(player.MobilePartyId, out party))
            return false;
        return hero != null && party != null;
    }

    private void PublishMutation(TournamentMutationStatus status, NetPeer requester, TournamentSessionSnapshot snapshot)
    {
        if (status == TournamentMutationStatus.Applied)
            BroadcastSnapshot(snapshot);
        else if (snapshot != null)
            SendCanonical(requester, snapshot);
    }

    private void BroadcastSnapshot(TournamentSessionSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        network.SendAll(new NetworkTournamentSessionSnapshot(snapshot));
        messageBroker.Publish(this, new TournamentSessionUpdated(snapshot));
    }

    private void SendCanonical(NetPeer peer, TournamentSessionSnapshot snapshot)
    {
        if (peer != null && snapshot != null)
            network.Send(peer, new NetworkTournamentSessionSnapshot(snapshot));
    }

    private void SendRejection(NetPeer peer, string townId, string reason)
    {
        if (peer != null)
            network.Send(peer, new NetworkTournamentRequestRejected(townId, reason));
    }

    private void SendBetResult(
        NetPeer peer,
        string sessionId,
        long revision,
        long sequence,
        string matchId,
        bool accepted,
        string reason,
        int bettedDenars,
        int thisRoundBettedDenars,
        int expectedPayout,
        bool isSettlement)
    {
        if (peer == null)
            return;

        network.Send(peer, new NetworkTournamentBetResult(
            sessionId,
            revision,
            sequence,
            matchId,
            accepted,
            reason,
            bettedDenars,
            thisRoundBettedDenars,
            expectedPayout,
            isSettlement));
    }

    private void ResolveBets(TournamentSessionSnapshot snapshot, string[] winnerSlotIds)
    {
        foreach (TournamentContestantData contestant in snapshot.Contestants.Where(contestant => contestant.IsHuman))
        {
            if (!IsSlotInCurrentMatch(snapshot, contestant.SlotId))
                continue;
            if (winnerSlotIds.Contains(contestant.SlotId))
                continue;
            SettleBetLedger(
                snapshot.SessionId,
                contestant.ControllerId,
                snapshot.Revision,
                snapshot.CurrentMatchId,
                "Tournament bet lost");
        }
    }

    private void CompleteTournament(TournamentSessionSnapshot snapshot)
    {
        if (!ShouldCompleteTournament(snapshot))
            return;
        if (!completionInProgress.Add(snapshot.SessionId))
            return;

        try
        {
            if (!TryResolveCompletionContext(
                    snapshot,
                    out var game,
                    out var winner,
                    out var participants,
                    out var winnerData,
                    out var manager))
            {
                Logger.Error("Could not rehydrate completed tournament session {SessionId}", snapshot.SessionId);
                return;
            }

            TournamentCompletionTransaction transaction = GetOrCreateCompletionTransaction(snapshot.SessionId);
            RunCompletionTransactions(
                snapshot,
                game,
                winner,
                participants,
                winnerData,
                manager,
                transaction);
            saveDeferral.Flush();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to complete tournament session {SessionId}", snapshot.SessionId);
        }
        finally
        {
            completionInProgress.Remove(snapshot.SessionId);
        }
    }

    private bool ShouldCompleteTournament(TournamentSessionSnapshot snapshot)
    {
        if (snapshot == null || !snapshot.IsCompleted)
            return false;
        return !completionTransactions.TryGetValue(snapshot.SessionId, out var existing) ||
            (!existing.IsCompleted && !existing.IsReadyForRemoval);
    }

    private bool TryResolveCompletionContext(
        TournamentSessionSnapshot snapshot,
        out FightTournamentGame game,
        out CharacterObject winner,
        out MBList<CharacterObject> participants,
        out TournamentContestantData winnerData,
        out TournamentManager manager)
    {
        game = null;
        winner = null;
        participants = null;
        winnerData = snapshot.Contestants
            .FirstOrDefault(contestant => contestant.SlotId == snapshot.WinnerSlotId);
        manager = Campaign.Current?.TournamentManager as TournamentManager;
        if (!tournamentGameInterface.TryRehydrateGame(snapshot, out game) ||
            !TryResolveWinner(snapshot, out winner, out participants) ||
            manager == null ||
            game.Town == null ||
            game.Prize == null ||
            winnerData == null)
            return false;

        return winnerData.ControllerId == null ||
            (winner.IsHero && winner.HeroObject?.PartyBelongedTo != null);
    }

    private TournamentCompletionTransaction GetOrCreateCompletionTransaction(string sessionId)
    {
        if (completionTransactions.TryGetValue(sessionId, out var transaction))
            return transaction;

        transaction = new TournamentCompletionTransaction();
        completionTransactions.Add(sessionId, transaction);
        return transaction;
    }

    private void RunCompletionTransactions(
        TournamentSessionSnapshot snapshot,
        FightTournamentGame game,
        CharacterObject winner,
        MBList<CharacterObject> participants,
        TournamentContestantData winnerData,
        TournamentManager manager,
        TournamentCompletionTransaction transaction)
    {
        transaction.Run(TournamentCompletionStep.Leaderboard, () =>
        {
            if (winner.IsHero)
                manager.AddLeaderboardEntry(winner.HeroObject);
        });
        transaction.Run(TournamentCompletionStep.Influence, () =>
        {
            if (winnerData.ControllerId != null &&
                Campaign.Current.GameMode == CampaignGameMode.Campaign &&
                winner.HeroObject.MapFaction?.IsKingdomFaction == true &&
                winner.HeroObject.MapFaction.Leader != winner.HeroObject)
            {
                GainKingdomInfluenceAction.ApplyForDefault(winner.HeroObject, 1f);
            }
        });
        transaction.Run(TournamentCompletionStep.Prize, () =>
        {
            if (!winner.IsHero)
                return;
            if (winnerData.ControllerId != null)
                winner.HeroObject.PartyBelongedTo.ItemRoster.AddToCounts(game.Prize, 1);
            else
                manager.GivePrizeToWinner(game, winner.HeroObject, true);
        });
        transaction.Run(TournamentCompletionStep.BetPayout,
            () => PayWinningBetAndForfeitOthers(snapshot, winner));
        transaction.Run(TournamentCompletionStep.BetSettlement, () => { });
        transaction.Run(TournamentCompletionStep.SimulationProgression, () =>
        {
            if (!liveCombatSessions.Contains(snapshot.SessionId))
                ApplyTournamentProgression(game.Town, participants);
        });
    }

    private void FinalizeCompletedTournament(TournamentSessionSnapshot snapshot)
    {
        CompleteTournament(snapshot);
        if (!TryBeginFinalization(snapshot, out var transaction))
            return;

        try
        {
            RunFinalizationTransactions(snapshot, transaction);
            Logger.Information(
                "[Tournament] Finalized completed tournament session={SessionId}; ejecting all mission members",
                snapshot.SessionId);
            saveDeferral.Flush();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to finalize tournament session {SessionId}", snapshot.SessionId);
        }
        finally
        {
            completionInProgress.Remove(snapshot.SessionId);
        }
    }

    private bool TryBeginFinalization(
        TournamentSessionSnapshot snapshot,
        out TournamentCompletionTransaction transaction)
    {
        transaction = null;
        return snapshot != null &&
            completionTransactions.TryGetValue(snapshot.SessionId, out transaction) &&
            transaction.IsReadyForRemoval &&
            !transaction.IsCompleted &&
            completionInProgress.Add(snapshot.SessionId);
    }

    private void RunFinalizationTransactions(
        TournamentSessionSnapshot snapshot,
        TournamentCompletionTransaction transaction)
    {
        if (!tournamentGameInterface.TryRehydrateGame(snapshot, out var game) ||
            !TryResolveWinner(snapshot, out var winner, out var participants) ||
            game.Town == null ||
            game.Prize == null)
        {
            throw new InvalidOperationException("Could not rehydrate the tournament before final removal.");
        }

        transaction.Run(TournamentCompletionStep.FinishedEvent, () =>
            CampaignEventDispatcher.Instance.OnTournamentFinished(
                winner,
                new MBReadOnlyList<CharacterObject>(participants),
                game.Town,
                game.Prize));
        transaction.Run(TournamentCompletionStep.NativeRemoval, () =>
        {
            if (!RemoveNativeTournament(snapshot))
                throw new InvalidOperationException("Could not remove the completed native tournament.");
        });
        transaction.Run(TournamentCompletionStep.SessionRemoval, () =>
        {
            if (!RemoveSessionAndBroadcast(snapshot))
                throw new InvalidOperationException("Could not remove the completed coop tournament session.");
        });
    }

    private void Handle_CampaignTick(MessagePayload<CampaignTick> payload)
    {
        if (ModInformation.IsClient)
            return;

        foreach (TournamentSessionSnapshot snapshot in sessionRegistry.GetAll()
                     .Where(session => session.IsCompleted))
        {
            CompleteTournament(snapshot);
        }
    }

    private bool RemoveNativeTournament(TournamentSessionSnapshot snapshot)
    {
        if (Campaign.Current?.TournamentManager is not TournamentManager manager ||
            !objectManager.TryGetObject(snapshot.TownId, out Town town))
        {
            return false;
        }

        TournamentGame nativeGame = manager.GetTournamentGame(town);
        if (nativeGame == null)
        {
            Logger.Information(
                "[Tournament] Completed native tournament already absent session={SessionId}, town={Town}, activeCount={ActiveCount}",
                snapshot.SessionId,
                town.Name,
                manager._activeTournaments.Count);
            return true;
        }

        int activeCountBefore = manager._activeTournaments.Count;
        Logger.Information(
            "[Tournament] Removing completed native tournament session={SessionId}, town={Town}, activeCountBefore={ActiveCountBefore}",
            snapshot.SessionId,
            town.Name,
            activeCountBefore);
        using (nativeRemovalAuthorization.Authorize(snapshot.TownId))
        {
            Logger.Information(
                "[Tournament] Authorized completion transaction native removal session={SessionId}, townId={TownId}",
                snapshot.SessionId,
                snapshot.TownId);
            manager.RemoveTournament(nativeGame);
        }
        bool removed = manager.GetTournamentGame(town) == null;
        Logger.Information(
            "[Tournament] Removed completed native tournament session={SessionId}, town={Town}, removed={Removed}, activeCountBefore={ActiveCountBefore}, activeCountAfter={ActiveCountAfter}",
            snapshot.SessionId,
            town.Name,
            removed,
            activeCountBefore,
            manager._activeTournaments.Count);
        return removed;
    }

    private bool TryResolveWinner(
        TournamentSessionSnapshot snapshot,
        out CharacterObject winner,
        out MBList<CharacterObject> participants)
    {
        winner = null;
        participants = new MBList<CharacterObject>();
        foreach (TournamentContestantData contestant in snapshot.Contestants)
        {
            if (!objectManager.TryGetObject(contestant.CharacterId, out CharacterObject character))
                return false;
            participants.Add(character);
            if (contestant.SlotId == snapshot.WinnerSlotId)
                winner = character;
        }
        return winner != null;
    }

    private static bool TryGetPlayerSlot(
        TournamentSessionSnapshot snapshot,
        string controllerId,
        out TournamentContestantData slot)
    {
        slot = snapshot.Contestants.FirstOrDefault(contestant =>
            contestant.IsHuman && !contestant.IsReplaced && contestant.ControllerId == controllerId);
        return slot != null;
    }

    private static bool IsConfirmedEntrant(TournamentSessionSnapshot snapshot, string controllerId)
    {
        return snapshot != null && (snapshot.HostControllerId == controllerId ||
            snapshot.SuccessorControllerIds.Contains(controllerId));
    }

    private static void LogSpectatorRequest(
        string controllerId,
        TournamentMutationStatus status,
        TournamentSessionSnapshot snapshot)
    {
        Logger.Information(
            "[Tournament] Spectator request session={SessionId}, controller={ControllerId}, status={Status}, humans={HumanCount}, spectators={SpectatorCount}, voters={VoterCount}, revision={Revision}",
            snapshot?.SessionId,
            controllerId,
            status,
            CountActiveHumans(snapshot),
            snapshot?.SpectatorControllerIds?.Length ?? 0,
            snapshot?.VoterCount ?? 0,
            snapshot?.Revision ?? 0);
    }

    private static int CountActiveHumans(TournamentSessionSnapshot snapshot)
        => snapshot?.Contestants?.Count(contestant => contestant.IsHuman && !contestant.IsReplaced) ?? 0;

    private static int CountEntrants(TournamentSessionSnapshot snapshot)
        => snapshot == null || snapshot.HostControllerId == null
            ? 0
            : 1 + (snapshot.SuccessorControllerIds?.Length ?? 0);

    private static string GetPlayerRole(TournamentSessionSnapshot snapshot, string controllerId)
    {
        if (snapshot?.Contestants?.Any(contestant =>
                contestant.IsHuman &&
                !contestant.IsReplaced &&
                contestant.ControllerId == controllerId) == true)
            return "Competitor";

        return snapshot?.SpectatorControllerIds?.Contains(controllerId) == true
            ? "Spectator"
            : "NonParticipant";
    }

    private static bool IsSlotInCurrentMatch(TournamentSessionSnapshot snapshot, string slotId)
    {
        return snapshot.Rounds
            .SelectMany(round => round.Matches)
            .Where(match => match.MatchId == snapshot.CurrentMatchId)
            .SelectMany(match => match.Teams)
            .Any(team => team.ParticipantSlotIds.Contains(slotId));
    }

    private bool HasValidManifestObjects(TournamentSpawnManifestData manifest)
    {
        foreach (TournamentAgentSpawnData agent in manifest.Agents)
        {
            if (!objectManager.TryGetObject(agent.CharacterId, out CharacterObject _) ||
                !HasValidEquipmentObjects(agent.Equipment))
            {
                return false;
            }

            if (agent.MountAgentId != Guid.Empty &&
                ((!string.IsNullOrEmpty(agent.MountCharacterId) &&
                  !objectManager.TryGetObject(agent.MountCharacterId, out BasicCharacterObject _)) ||
                 !HasValidEquipmentObjects(agent.MountEquipment)))
            {
                return false;
            }
        }
        return true;
    }

    private bool HasValidEquipmentObjects(TournamentEquipmentElementData[] equipment)
    {
        foreach (TournamentEquipmentElementData element in equipment)
        {
            if (!objectManager.TryGetObject(element.ItemId, out ItemObject _) ||
                (!string.IsNullOrEmpty(element.ItemModifierId) &&
                 !objectManager.TryGetObject(element.ItemModifierId, out ItemModifier _)))
            {
                return false;
            }
        }
        return true;
    }

    private void SettleBetLedger(
        string sessionId,
        string controllerId,
        long revision,
        string matchId,
        string reason,
        NetPeer peer = null)
    {
        string key = GetBetKey(sessionId, controllerId);
        if (!betLedger.TryGetValue(key, out var ledger))
            return;

        peer ??= tournamentPeerControllers
            .FirstOrDefault(entry => entry.Value == controllerId)
            .Key;
        long sequence = Math.Max(ledger.LastRequestSequence, ledger.LastResponseSequence) + 1;
        betLedger.Remove(key);
        SendBetResult(
            peer,
            sessionId,
            revision,
            sequence,
            matchId,
            true,
            reason,
            0,
            0,
            0,
            true);
    }

    private static string GetBetKey(string sessionId, string controllerId)
    {
        return $"{sessionId}:{controllerId}";
    }
    private sealed class BetLedgerEntry
    {
        public readonly Dictionary<string, int> MatchAmounts = new();
        public int ExpectedPayout;
        public int TotalBettedDenars;
        public long LastRequestSequence;
        public long LastResponseSequence;
        public long LastResponseRevision;
        public bool HasResponse;
        public bool LastAccepted;
        public string LastReason;
        public int LastBettedDenars;
        public int LastThisRoundBettedDenars;
        public int LastExpectedPayout;
        public string LastMatchId;
        public bool LastIsSettlement;
    }
}
