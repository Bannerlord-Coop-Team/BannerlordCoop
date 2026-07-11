using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using LiteNetLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;

namespace GameInterface.Services.Tournaments.Handlers;

internal sealed class TournamentStateSyncHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TournamentStateSyncHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ITournamentSessionRegistry sessionRegistry;
    private readonly IRelayNetwork[] relayNetworks;

    public TournamentStateSyncHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ITournamentSessionRegistry sessionRegistry,
        IEnumerable<IRelayNetwork> relayNetworks)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.sessionRegistry = sessionRegistry;
        this.relayNetworks = relayNetworks?.ToArray() ?? new IRelayNetwork[0];

        messageBroker.Subscribe<CampaignReady>(Handle_CampaignReady);
        messageBroker.Subscribe<NetworkRequestTournamentState>(Handle_StateRequest);
        messageBroker.Subscribe<NetworkTournamentStateSnapshot>(Handle_StateSnapshot);
        messageBroker.Subscribe<NetworkTournamentSessionRemoved>(Handle_SessionRemoved);
        messageBroker.Subscribe<TournamentNativeStateChanged>(Handle_NativeStateChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignReady>(Handle_CampaignReady);
        messageBroker.Unsubscribe<NetworkRequestTournamentState>(Handle_StateRequest);
        messageBroker.Unsubscribe<NetworkTournamentStateSnapshot>(Handle_StateSnapshot);
        messageBroker.Unsubscribe<NetworkTournamentSessionRemoved>(Handle_SessionRemoved);
        messageBroker.Unsubscribe<TournamentNativeStateChanged>(Handle_NativeStateChanged);
    }

    private void Handle_CampaignReady(MessagePayload<CampaignReady> payload)
    {
        if (ModInformation.IsClient)
            network.SendAll(new NetworkRequestTournamentState());
    }

    private void Handle_StateRequest(MessagePayload<NetworkRequestTournamentState> payload)
    {
        if (ModInformation.IsClient ||
            payload.Who is not NetPeer peer ||
            !playerManager.TryGetPlayer(peer, out _))
        {
            return;
        }

        GameThread.RunSafe(
            () => network.Send(peer, CreateStateSnapshot()),
            context: nameof(Handle_StateRequest));
    }

    private void Handle_NativeStateChanged(MessagePayload<TournamentNativeStateChanged> payload)
    {
        if (ModInformation.IsClient)
            return;

        GameThread.RunSafe(() =>
        {
            NetworkTournamentStateSnapshot snapshot = CreateStateSnapshot();
            Logger.Information(
                "[Tournament] Broadcasting native tournament state: tournaments={TournamentCount}, towns={TournamentTowns}, sessions={SessionCount}",
                snapshot.NativeTournaments.Length,
                string.Join(",", snapshot.NativeTournaments.Select(tournament => tournament.TownId)),
                snapshot.Sessions.Length);
            network.SendAll(snapshot);
        }, context: nameof(Handle_NativeStateChanged));
    }

    private void Handle_StateSnapshot(MessagePayload<NetworkTournamentStateSnapshot> payload)
    {
        if (ModInformation.IsServer ||
            !TournamentServerMessageGuard.IsTrusted(payload.Who, relayNetworks))
            return;

        GameThread.RunSafe(() =>
        {
            if (Campaign.Current?.TournamentManager is not TournamentManager manager)
                return;

            TournamentNativeGameData[] nativeTournaments = payload.What.NativeTournaments ??
                new TournamentNativeGameData[0];
            TournamentLeaderboardEntryData[] leaderboard = payload.What.Leaderboard ??
                new TournamentLeaderboardEntryData[0];
            TournamentSessionSnapshot[] sessions = payload.What.Sessions ??
                new TournamentSessionSnapshot[0];
            Logger.Information(
                "[Tournament] Received native tournament state: authoritative={AuthoritativeCount}, authoritativeTowns={AuthoritativeTowns}, localBefore={LocalCount}, localTownsBefore={LocalTowns}",
                nativeTournaments.Length,
                string.Join(",", nativeTournaments
                    .Where(tournament => tournament != null)
                    .Select(tournament => tournament.TownId)),
                manager._activeTournaments.Count,
                string.Join(",", manager._activeTournaments
                    .Where(tournament => tournament?.Town != null)
                    .Select(tournament => tournament.Town.Name.ToString())));

            foreach (TournamentSessionSnapshot stale in TournamentStateReconciliation.GetStaleSessions(
                         sessionRegistry.GetAll(), sessions))
            {
                if (!sessionRegistry.Remove(stale.SessionId))
                    continue;
                messageBroker.Publish(this, new TournamentSessionRemoved(stale.SessionId, stale.TownId));
            }

            var authoritativeTournaments = new Dictionary<Town, FightTournamentGame>();
            foreach (TournamentNativeGameData data in nativeTournaments)
            {
                if (data == null || !TryRehydrateNativeGame(data, out var game))
                    continue;
                authoritativeTournaments[game.Town] = game;
            }

            foreach (TournamentGame tournament in manager._activeTournaments.ToArray())
            {
                if (tournament?.Town != null && authoritativeTournaments.ContainsKey(tournament.Town))
                    continue;

                Town removedTown = tournament?.Town;
                manager.RemoveTournament(tournament);
                bool removed = !manager._activeTournaments.Contains(tournament);
                if (removed && removedTown != null)
                {
                    CampaignEventDispatcher.Instance.OnTournamentCancelled(removedTown);
                    Logger.Information(
                        "[Tournament] Raised native tournament cancellation event after authoritative removal from {Town}",
                        removedTown.Name);
                }
                Logger.Information(
                    "[Tournament] Removed native tournament from {Town}; removed={Removed}",
                    removedTown?.Name,
                    removed);
            }
            foreach (var pair in authoritativeTournaments)
            {
                TournamentGame existing = manager._activeTournaments
                    .FirstOrDefault(tournament => tournament?.Town == pair.Key);
                if (existing != null)
                {
                    existing.CreationTime = pair.Value.CreationTime;
                    existing.Mode = pair.Value.Mode;
                    existing.Prize = pair.Value.Prize;
                    continue;
                }

                manager.AddTournament(pair.Value);
                Logger.Information("[Tournament] Added native tournament to {Town}", pair.Key.Name);
            }
            Logger.Information(
                "[Tournament] Reconciled native tournament state: authoritative={AuthoritativeCount}, localAfter={LocalCount}, localTownsAfter={LocalTowns}",
                authoritativeTournaments.Count,
                manager._activeTournaments.Count,
                string.Join(",", manager._activeTournaments
                    .Where(tournament => tournament?.Town != null)
                    .Select(tournament => tournament.Town.Name.ToString())));

            manager._worldWideTournamentLeaderboard.Clear();
            foreach (TournamentLeaderboardEntryData data in leaderboard)
            {
                if (data != null && objectManager.TryGetObject(data.HeroId, out Hero hero))
                    manager.InitializeLeaderboardEntry(hero, data.Wins);
            }

            foreach (TournamentSessionSnapshot snapshot in sessions)
            {
                TournamentSessionSnapshot normalized = TournamentSessionSnapshotNormalizer.Normalize(snapshot);
                if (normalized != null && sessionRegistry.ApplySnapshot(normalized))
                    messageBroker.Publish(this, new TournamentSessionUpdated(normalized));
            }
        }, context: nameof(Handle_StateSnapshot));
    }

    private void Handle_SessionRemoved(MessagePayload<NetworkTournamentSessionRemoved> payload)
    {
        if (ModInformation.IsServer ||
            !TournamentServerMessageGuard.IsTrusted(payload.Who, relayNetworks))
            return;

        GameThread.RunSafe(() =>
        {
            sessionRegistry.Remove(payload.What.SessionId);
            messageBroker.Publish(this, new TournamentSessionRemoved(
                payload.What.SessionId,
                payload.What.TownId));
        }, context: nameof(Handle_SessionRemoved));
    }

    private NetworkTournamentStateSnapshot CreateStateSnapshot()
    {
        var tournaments = new List<TournamentNativeGameData>();
        if (Campaign.Current?.TournamentManager is TournamentManager manager)
        {
            foreach (TournamentGame tournament in manager._activeTournaments)
            {
                if (tournament?.Town == null || !objectManager.TryGetId(tournament.Town, out var townId))
                {
                    continue;
                }

                bool isSupported = tournament.GetType() == typeof(FightTournamentGame);
                string prizeId = null;
                if (tournament.Prize != null && !objectManager.TryGetId(tournament.Prize, out prizeId))
                {
                    if (isSupported)
                        continue;
                    prizeId = null;
                }
                tournaments.Add(new TournamentNativeGameData(
                    townId,
                    prizeId,
                    tournament.CreationTime,
                    (int)tournament.Mode,
                    isSupported));
            }
        }

        TournamentLeaderboardEntryData[] leaderboard = Campaign.Current?.TournamentManager?.GetLeaderboard()
            ?.Select(entry =>
            {
                return objectManager.TryGetId(entry.Key, out var heroId)
                    ? new TournamentLeaderboardEntryData(heroId, entry.Value)
                    : null;
            })
            .Where(entry => entry != null)
            .ToArray() ?? new TournamentLeaderboardEntryData[0];
        return new NetworkTournamentStateSnapshot(
            tournaments.ToArray(),
            leaderboard,
            sessionRegistry.GetAll());
    }

    private bool TryRehydrateNativeGame(TournamentNativeGameData data, out FightTournamentGame game)
    {
        game = null;
        if (!objectManager.TryGetObject(data.TownId, out Town town))
            return false;

        ItemObject prize = null;
        if (data.PrizeItemId != null && !objectManager.TryGetObject(data.PrizeItemId, out prize))
            return false;

        game = data.IsSupported
            ? ObjectHelper.SkipConstructor<FightTournamentGame>()
            : ObjectHelper.SkipConstructor<UnsupportedFightTournamentGame>();
        game.Town = town;
        game.CreationTime = data.CreationTime;
        game.Mode = (TournamentGame.QualificationMode)data.QualificationMode;
        game.Prize = prize;
        return true;

    }
    private sealed class UnsupportedFightTournamentGame : FightTournamentGame
    {
        private UnsupportedFightTournamentGame(Town town) : base(town)
        {
        }
    }
}
