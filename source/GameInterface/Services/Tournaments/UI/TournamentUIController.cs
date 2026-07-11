using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Tournaments.UI;

internal sealed class TournamentUIController : ITournamentUIController, IHandler
{
    internal enum MenuRoute
    {
        Refresh,
        Preparation,
        Active,
        TownCenter
    }

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly ConcurrentDictionary<string, TournamentSessionSnapshot> sessionsById = new();
    private readonly IRelayNetwork[] relayNetworks;
    private readonly ConcurrentDictionary<string, TournamentSessionSnapshot> sessionsByTownId = new();
    private readonly ConcurrentDictionary<string, long> betSequencesBySessionId = new();
    private readonly ConcurrentDictionary<string, byte> pendingPreparationLeaves = new();
    private readonly ConcurrentDictionary<string, byte> pendingActiveLeaves = new();

    public event Action<TournamentSessionSnapshot> StateChanged;
    public event Action<string> SessionRemoved;
    public event Action<NetworkTournamentBetResult> BetResultReceived;

    public string LocalControllerId => controllerIdProvider.ControllerId;

    public TournamentUIController(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IControllerIdProvider controllerIdProvider,
        IEnumerable<IRelayNetwork> relayNetworks = null)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.controllerIdProvider = controllerIdProvider;
        this.relayNetworks = relayNetworks?.ToArray() ?? Array.Empty<IRelayNetwork>();

        messageBroker.Subscribe<TournamentSessionUpdated>(Handle_TournamentSessionUpdated);
        messageBroker.Subscribe<TournamentSessionRemoved>(Handle_TournamentSessionRemoved);
        messageBroker.Subscribe<NetworkTournamentBetResult>(Handle_NetworkTournamentBetResult);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<TournamentSessionUpdated>(Handle_TournamentSessionUpdated);
        messageBroker.Unsubscribe<TournamentSessionRemoved>(Handle_TournamentSessionRemoved);
        messageBroker.Unsubscribe<NetworkTournamentBetResult>(Handle_NetworkTournamentBetResult);
        sessionsById.Clear();
        sessionsByTownId.Clear();
        betSequencesBySessionId.Clear();
        pendingPreparationLeaves.Clear();
        pendingActiveLeaves.Clear();
        StateChanged = null;
        SessionRemoved = null;
        BetResultReceived = null;
    }

    public bool TryGetTownSession(string townId, out TournamentSessionSnapshot snapshot)
    {
        snapshot = null;
        return !string.IsNullOrEmpty(townId) && sessionsByTownId.TryGetValue(townId, out snapshot);
    }

    public string GetPreparationPrizeName(string townId)
    {
        if (!TryGetTownSession(townId, out var snapshot) || string.IsNullOrEmpty(snapshot.PrizeItemId))
            return new TaleWorlds.Localization.TextObject("{=coop_tournament_no_prize}No prize").ToString();

        if (objectManager.TryGetObject<ItemObject>(snapshot.PrizeItemId, out var item))
            return item.Name.ToString();

        return new TaleWorlds.Localization.TextObject("{=coop_tournament_unknown_prize}Unknown prize").ToString();
    }

    public string GetPreparationPlayerNames(string townId)
    {
        if (!TryGetTownSession(townId, out var snapshot))
            return string.Empty;

        var names = snapshot.Contestants
            .Where(contestant => contestant.IsHuman && !contestant.IsReplaced)
            .Select(contestant => contestant.DisplayName)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToArray();

        return names.Length == 0
            ? new TaleWorlds.Localization.TextObject("{=coop_tournament_no_competitors}No players enrolled").ToString()
            : string.Join("\n", names);
    }

    public bool CanStartPreparation(string townId)
        => TryGetTownSession(townId, out var snapshot) &&
           snapshot.Phase == TournamentSessionPhase.Preparation &&
           IsActiveLocalContestant(snapshot);

    public bool CanLeavePreparation(string townId)
        => TryGetTownSession(townId, out var snapshot) &&
           snapshot.Phase == TournamentSessionPhase.Preparation &&
           IsActiveLocalContestant(snapshot);

    public bool CanSpectate(string townId)
        => TryGetTownSession(townId, out var snapshot) &&
           IsActive(snapshot) &&
           !IsActiveLocalContestant(snapshot);

    public void RequestJoin(string townId, string sessionId, long expectedRevision)
        => network.SendAll(new NetworkRequestJoinTournament(townId, sessionId, expectedRevision));

    public void RequestStart(string townId)
    {
        if (!TryGetTownSession(townId, out var snapshot)) return;

        network.SendAll(new NetworkRequestStartTournament(snapshot.SessionId, snapshot.Revision));
    }

    public void RequestLeavePreparation(string townId)
    {
        if (!TryGetTownSession(townId, out var snapshot)) return;

        pendingPreparationLeaves[snapshot.SessionId] = 0;
        SendLeavePreparation(snapshot);
    }

    public void RequestSpectate(string townId)
    {
        if (!TryGetTownSession(townId, out var snapshot)) return;

        network.SendAll(new NetworkRequestSpectateTournament(snapshot.SessionId, snapshot.Revision));
    }

    public void RequestLeaveActive(TournamentSessionSnapshot snapshot)
    {
        if (snapshot == null) return;

        pendingActiveLeaves[snapshot.SessionId] = 0;
        SendLeaveActive(snapshot);
    }

    public void RequestChoice(TournamentSessionSnapshot snapshot, TournamentPlayerChoice choice)
    {
        if (snapshot == null || string.IsNullOrEmpty(snapshot.CurrentMatchId)) return;

        network.SendAll(new NetworkRequestTournamentChoice(
            snapshot.SessionId,
            snapshot.Revision,
            snapshot.CurrentMatchId,
            choice));
    }

    public void RequestBet(TournamentSessionSnapshot snapshot, int amount)
    {
        if (snapshot == null || string.IsNullOrEmpty(snapshot.CurrentMatchId)) return;

        long sequence = betSequencesBySessionId.AddOrUpdate(
            snapshot.SessionId,
            1,
            (_, currentSequence) => currentSequence + 1);
        network.SendAll(new NetworkRequestTournamentBet(
            snapshot.SessionId,
            snapshot.Revision,
            snapshot.CurrentMatchId,
            amount,
            sequence));
    }

    private void Handle_TournamentSessionUpdated(MessagePayload<TournamentSessionUpdated> payload)
    {
        var snapshot = payload.What.Snapshot;
        if (!CacheSnapshot(snapshot))
            return;

        GameThread.RunSafe(() =>
        {
            RetryPendingPreparationLeave(snapshot);
            RetryPendingActiveLeave(snapshot);
            StateChanged?.Invoke(snapshot);
            RouteMenu(snapshot);
        });
    }

    private void Handle_TournamentSessionRemoved(MessagePayload<TournamentSessionRemoved> payload)
    {
        bool removed = RemoveSession(payload.What.SessionId, payload.What.TownId);
        GameThread.RunSafe(() =>
        {
            if (removed)
                SessionRemoved?.Invoke(payload.What.SessionId);
            RouteRemovedSession(payload.What.TownId);
        });
    }

    internal bool CacheSnapshot(TournamentSessionSnapshot snapshot)
    {
        snapshot = TournamentSessionSnapshotNormalizer.Normalize(snapshot);
        if (snapshot == null || string.IsNullOrEmpty(snapshot.SessionId) || string.IsNullOrEmpty(snapshot.TownId))
            return false;
        if (sessionsById.TryGetValue(snapshot.SessionId, out var existing) &&
            existing.Revision >= snapshot.Revision)
        {
            return false;
        }

        if (sessionsByTownId.TryGetValue(snapshot.TownId, out var oldTownSession) &&
            oldTownSession.SessionId != snapshot.SessionId)
        {
            sessionsById.TryRemove(oldTownSession.SessionId, out _);
        }

        sessionsById[snapshot.SessionId] = snapshot;
        sessionsByTownId[snapshot.TownId] = snapshot;
        return true;
    }

    internal bool RemoveSession(string sessionId, string townId)
    {
        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(townId))
            return false;

        bool removed = sessionsById.TryRemove(sessionId, out _);
        betSequencesBySessionId.TryRemove(sessionId, out _);
        pendingPreparationLeaves.TryRemove(sessionId, out _);
        pendingActiveLeaves.TryRemove(sessionId, out _);
        if (sessionsByTownId.TryGetValue(townId, out var townSession) &&
            townSession.SessionId == sessionId)
        {
            removed |= sessionsByTownId.TryRemove(townId, out _);
        }

        return removed;
    }

    internal void RetryPendingPreparationLeave(TournamentSessionSnapshot snapshot)
    {
        if (!pendingPreparationLeaves.ContainsKey(snapshot.SessionId))
            return;

        if (snapshot.Phase == TournamentSessionPhase.Preparation &&
            IsActiveLocalContestant(snapshot))
        {
            SendLeavePreparation(snapshot);
        }
        else
        {
            pendingPreparationLeaves.TryRemove(snapshot.SessionId, out _);
        }
    }

    private void SendLeavePreparation(TournamentSessionSnapshot snapshot)
    {
        network.SendAll(new NetworkRequestLeaveTournamentPreparation(
            snapshot.SessionId,
            snapshot.Revision));
    }

    internal void RetryPendingActiveLeave(TournamentSessionSnapshot snapshot)
    {
        if (!pendingActiveLeaves.ContainsKey(snapshot.SessionId))
            return;

        if (IsLocalMissionMember(snapshot))
            SendLeaveActive(snapshot);
        else
            pendingActiveLeaves.TryRemove(snapshot.SessionId, out _);
    }

    private void SendLeaveActive(TournamentSessionSnapshot snapshot)
    {
        network.SendAll(new NetworkRequestLeaveActiveTournament(
            snapshot.SessionId,
            snapshot.Revision));
    }

    private void RouteRemovedSession(string townId)
    {
        if (ModInformation.IsServer)
            return;

        var town = Settlement.CurrentSettlement?.Town;
        var menuContext = Campaign.Current?.CurrentMenuContext;
        if (town == null ||
            menuContext?.GameMenu == null ||
            !objectManager.TryGetId(town, out var currentTownId))
        {
            return;
        }

        if (GetRemovedSessionMenuRoute(
                currentTownId == townId,
                menuContext.GameMenu.StringId) == MenuRoute.TownCenter)
        {
            GameMenu.SwitchToMenu(CoopTournamentCampaignBehavior.TownCenterMenuId);
        }
        else
        {
            menuContext.Refresh();
        }
    }

    internal static MenuRoute GetRemovedSessionMenuRoute(bool isCurrentTown, string menuId)
    {
        return isCurrentTown &&
               (menuId == CoopTournamentCampaignBehavior.PreparationMenuId ||
                menuId == CoopTournamentCampaignBehavior.ActiveMenuId)
            ? MenuRoute.TownCenter
            : MenuRoute.Refresh;
    }

    private void RouteMenu(TournamentSessionSnapshot snapshot)
    {
        if (ModInformation.IsServer)
            return;

        var town = Settlement.CurrentSettlement?.Town;
        var menuContext = Campaign.Current?.CurrentMenuContext;
        if (town == null ||
            menuContext?.GameMenu == null ||
            !objectManager.TryGetId(town, out var currentTownId))
        {
            return;
        }

        var route = GetMenuRoute(
            currentTownId == snapshot.TownId,
            menuContext.GameMenu.StringId,
            snapshot.Phase,
            snapshot.IsCompleted,
            IsActiveLocalContestant(snapshot));
        switch (route)
        {
            case MenuRoute.Preparation:
                GameMenu.SwitchToMenu(CoopTournamentCampaignBehavior.PreparationMenuId);
                break;
            case MenuRoute.Active:
                GameMenu.SwitchToMenu(CoopTournamentCampaignBehavior.ActiveMenuId);
                break;
            case MenuRoute.TownCenter:
                GameMenu.SwitchToMenu(CoopTournamentCampaignBehavior.TownCenterMenuId);
                break;
            default:
                menuContext.Refresh();
                break;
        }
    }

    internal static MenuRoute GetMenuRoute(
        bool isCurrentTown,
        string menuId,
        TournamentSessionPhase phase,
        bool isCompleted,
        bool isLocalContestant)
    {
        if (!isCurrentTown)
            return MenuRoute.Refresh;

        if (phase == TournamentSessionPhase.Preparation &&
            isLocalContestant &&
            menuId == CoopTournamentCampaignBehavior.TownArenaMenuId)
        {
            return MenuRoute.Preparation;
        }

        if (phase == TournamentSessionPhase.Preparation &&
            !isLocalContestant &&
            menuId == CoopTournamentCampaignBehavior.PreparationMenuId)
        {
            return MenuRoute.TownCenter;
        }

        if (IsActive(phase) &&
            !isLocalContestant &&
            (menuId == CoopTournamentCampaignBehavior.TownArenaMenuId ||
             menuId == CoopTournamentCampaignBehavior.PreparationMenuId))
        {
            return MenuRoute.Active;
        }

        if (isCompleted &&
            (menuId == CoopTournamentCampaignBehavior.PreparationMenuId ||
             menuId == CoopTournamentCampaignBehavior.ActiveMenuId))
        {
            return MenuRoute.TownCenter;
        }

        return MenuRoute.Refresh;
    }

    private void Handle_NetworkTournamentBetResult(MessagePayload<NetworkTournamentBetResult> payload)
    {
        if (ModInformation.IsServer ||
            !TournamentServerMessageGuard.IsTrusted(payload.Who, relayNetworks))
            return;

        GameThread.RunSafe(() => BetResultReceived?.Invoke(payload.What));
    }

    private bool IsActiveLocalContestant(TournamentSessionSnapshot snapshot)
        => snapshot.Contestants.Any(contestant =>
            contestant.IsHuman &&
            !contestant.IsReplaced &&
            contestant.ControllerId == LocalControllerId);

    private bool IsLocalMissionMember(TournamentSessionSnapshot snapshot)
        => IsActiveLocalContestant(snapshot) ||
           snapshot.SpectatorControllerIds.Contains(LocalControllerId);

    private static bool IsActive(TournamentSessionSnapshot snapshot)
        => IsActive(snapshot.Phase);

    private static bool IsActive(TournamentSessionPhase phase)
        => phase == TournamentSessionPhase.AwaitingChoices ||
           phase == TournamentSessionPhase.LiveMatch ||
           phase == TournamentSessionPhase.SimulatingMatch;
}
