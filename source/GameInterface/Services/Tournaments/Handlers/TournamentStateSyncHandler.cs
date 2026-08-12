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
using System;
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
        this.relayNetworks = relayNetworks?.ToArray() ?? Array.Empty<IRelayNetwork>();

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
            () =>
            {
                if (TryCreateStateSnapshot(out var snapshot))
                    network.Send(peer, snapshot);
            },
            context: nameof(Handle_StateRequest));
    }

    private void Handle_NativeStateChanged(MessagePayload<TournamentNativeStateChanged> payload)
    {
        if (ModInformation.IsClient)
            return;

        GameThread.RunSafe(() =>
        {
            if (!TryCreateStateSnapshot(
                    out NetworkTournamentStateSnapshot snapshot))
                return;
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

        GameThread.RunSafe(
            () => ApplyStateSnapshot(payload.What),
            context: nameof(Handle_StateSnapshot));
    }

    private void ApplyStateSnapshot(NetworkTournamentStateSnapshot state)
    {
        if (Campaign.Current?.TournamentManager is not TournamentManager manager)
            return;

        TournamentNativeGameData[] nativeTournaments = state.NativeTournaments ??
            System.Array.Empty<TournamentNativeGameData>();
        TournamentLeaderboardEntryData[] leaderboard = state.Leaderboard ??
            System.Array.Empty<TournamentLeaderboardEntryData>();
        TournamentSessionSnapshot[] sessions = state.Sessions ??
            System.Array.Empty<TournamentSessionSnapshot>();
        LogReceivedState(manager, nativeTournaments);
        RemoveStaleSessions(sessions);

        Dictionary<Town, FightTournamentGame> authoritativeTournaments =
            RehydrateAuthoritativeTournaments(
                nativeTournaments,
                out HashSet<Town> authoritativeTowns);
        ReconcileNativeTournaments(
            manager,
            authoritativeTournaments,
            authoritativeTowns);
        ReconcileLeaderboard(manager, leaderboard);
        ApplySessions(sessions);
    }

    private static void LogReceivedState(
        TournamentManager manager,
        TournamentNativeGameData[] nativeTournaments)
    {
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
    }

    private void RemoveStaleSessions(TournamentSessionSnapshot[] sessions)
    {
        foreach (TournamentSessionSnapshot stale in TournamentStateReconciliation.GetStaleSessions(
                     sessionRegistry.GetAll(), sessions))
        {
            if (!sessionRegistry.Remove(stale.SessionId))
                continue;
            messageBroker.Publish(this, new TournamentSessionRemoved(stale.SessionId, stale.TownId));
        }
    }

    private Dictionary<Town, FightTournamentGame> RehydrateAuthoritativeTournaments(
        TournamentNativeGameData[] nativeTournaments,
        out HashSet<Town> authoritativeTowns)
    {
        var authoritativeTournaments = new Dictionary<Town, FightTournamentGame>();
        authoritativeTowns = new HashSet<Town>();
        foreach (TournamentNativeGameData data in nativeTournaments)
        {
            if (data == null || !objectManager.TryGetObject(data.TownId, out Town town))
                continue;
            authoritativeTowns.Add(town);
            // Unsupported/custom tournament rows are presence markers only.
            // Coop cannot safely reconstruct their implementation-specific
            // state, so never replace or mutate the native local instance.
            if (!data.IsSupported)
                continue;
            if (!TryRehydrateNativeGame(data, out var game))
            {
                Logger.Error(
                    "[Tournament] Preserving existing tournament in {Town}; authoritative entry could not yet be rehydrated",
                    town.Name);
                continue;
            }
            authoritativeTournaments[game.Town] = game;
        }
        return authoritativeTournaments;
    }

    private static void ReconcileNativeTournaments(
        TournamentManager manager,
        IReadOnlyDictionary<Town, FightTournamentGame> authoritativeTournaments,
        ISet<Town> authoritativeTowns)
    {
        RemoveStaleNativeTournaments(manager, authoritativeTowns);
        AddOrUpdateNativeTournaments(manager, authoritativeTournaments);
        Logger.Information(
            "[Tournament] Reconciled native tournament state: authoritative={AuthoritativeCount}, localAfter={LocalCount}, localTownsAfter={LocalTowns}",
            authoritativeTournaments.Count,
            manager._activeTournaments.Count,
            string.Join(",", manager._activeTournaments
                .Where(tournament => tournament?.Town != null)
                .Select(tournament => tournament.Town.Name.ToString())));
    }

    private static void RemoveStaleNativeTournaments(
        TournamentManager manager,
        ISet<Town> authoritativeTowns)
    {
        foreach (TournamentGame tournament in manager._activeTournaments.ToArray())
        {
            if (tournament?.Town != null && authoritativeTowns.Contains(tournament.Town))
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
    }

    private static void AddOrUpdateNativeTournaments(
        TournamentManager manager,
        IReadOnlyDictionary<Town, FightTournamentGame> authoritativeTournaments)
    {
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
    }

    private void ReconcileLeaderboard(
        TournamentManager manager,
        TournamentLeaderboardEntryData[] leaderboard)
    {
        manager._worldWideTournamentLeaderboard.Clear();
        foreach (TournamentLeaderboardEntryData data in leaderboard)
        {
            if (data != null && objectManager.TryGetObject(data.HeroId, out Hero hero))
                manager.InitializeLeaderboardEntry(hero, data.Wins);
        }
    }

    private void ApplySessions(TournamentSessionSnapshot[] sessions)
    {
        foreach (TournamentSessionSnapshot snapshot in sessions)
        {
            TournamentSessionSnapshot normalized = TournamentSessionSnapshotNormalizer.Normalize(snapshot);
            if (normalized != null && sessionRegistry.ApplySnapshot(normalized))
                messageBroker.Publish(this, new TournamentSessionUpdated(normalized));
        }
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

    private bool TryCreateStateSnapshot(
        out NetworkTournamentStateSnapshot snapshot)
    {
        snapshot = default;
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
                if (isSupported && tournament.Prize == null)
                {
                    Logger.Error(
                        "[Tournament] Supported tournament in {Town} has no prize; preserving client state until the authoritative prize exists",
                        tournament.Town.Name);
                    return false;
                }
                string prizeId = null;
                if (tournament.Prize != null &&
                    !StaticObjectRegistration.TryEnsure(
                        objectManager,
                        tournament.Prize,
                        out prizeId))
                {
                    if (isSupported)
                    {
                        Logger.Error(
                            "[Tournament] Could not register prize {Prize} for supported tournament in {Town}; preserving client state until it can be resolved",
                            tournament.Prize.StringId,
                            tournament.Town.Name);
                        return false;
                    }

                    // Unsupported tournament implementations are carried only so the client knows
                    // that the authoritative town is occupied.  Their prize is not rehydrated by
                    // Coop, so one unregistered custom prize must not suppress every other native
                    // tournament and session in the snapshot.
                    Logger.Warning(
                        "[Tournament] Could not register prize {Prize} for unsupported tournament in {Town}; publishing the town-presence entry without a prize",
                        tournament.Prize.StringId,
                        tournament.Town.Name);
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
            .ToArray() ?? Array.Empty<TournamentLeaderboardEntryData>();
        snapshot = new NetworkTournamentStateSnapshot(
            tournaments.ToArray(),
            leaderboard,
            sessionRegistry.GetAll());
        return true;
    }

    private bool TryRehydrateNativeGame(TournamentNativeGameData data, out FightTournamentGame game)
    {
        game = null;
        if (!objectManager.TryGetObject(data.TownId, out Town town))
            return false;

        ItemObject prize = null;
        if (data.IsSupported && string.IsNullOrEmpty(data.PrizeItemId))
            return false;
        if (data.PrizeItemId != null &&
            !StaticObjectRegistration.TryResolve(objectManager, data.PrizeItemId, out prize))
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
