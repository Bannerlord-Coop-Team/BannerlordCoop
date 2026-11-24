using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Hideouts;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents.Messages;
using Helpers;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.SiegeEvents.Handlers;

/// <summary>
/// Runs the keep (lord's hall) stage of a player-led siege assault. When the walls mission ends with the defenders
/// falling back, the server seats the walls players in vanilla's hall limits and gives them the hideout troop picker
/// through the encounter's Attack option. Once everyone answered, or the selection window ran out, the roster locks
/// and the seated players request the keep mission like any other battle.
/// </summary>
internal sealed class SiegeKeepHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeKeepHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly INetworkConfig configuration;
    private readonly ISiegeKeepTroopSelection selection;
    private readonly IBattleTroopReserveBuilder reserveBuilder;
    private readonly ConcurrentDictionary<string, PendingEntry> pendingEntries = new();

    /// <summary>The same 90 s the hideout assault waits for its leader, so a player who never answers can't stall the keep.</summary>
    internal TimeSpan SelectionWindow { get; set; } = TimeSpan.FromSeconds(90);

    public SiegeKeepHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager,
        IPlayerManager playerManager, INetworkConfig configuration, ISiegeKeepTroopSelection selection,
        IBattleTroopReserveBuilder reserveBuilder)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.configuration = configuration;
        this.selection = selection;
        this.reserveBuilder = reserveBuilder;
        messageBroker.Subscribe<AuthoritativeSiegeKeepRequested>(Handle_AuthoritativeSiegeKeepRequested);
        messageBroker.Subscribe<NetworkSiegeKeepEntryRequest>(Handle_EntryRequest);
        messageBroker.Subscribe<NetworkSiegeKeepEntryReply>(Handle_EntryReply);
        messageBroker.Subscribe<NetworkSiegeKeepPhaseChanged>(Handle_PhaseChanged);
        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
        messageBroker.Subscribe<MapEventFinalized>(Handle_MapEventFinalized);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AuthoritativeSiegeKeepRequested>(Handle_AuthoritativeSiegeKeepRequested);
        messageBroker.Unsubscribe<NetworkSiegeKeepEntryRequest>(Handle_EntryRequest);
        messageBroker.Unsubscribe<NetworkSiegeKeepEntryReply>(Handle_EntryReply);
        messageBroker.Unsubscribe<NetworkSiegeKeepPhaseChanged>(Handle_PhaseChanged);
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
        messageBroker.Unsubscribe<MapEventFinalized>(Handle_MapEventFinalized);
    }

    private void Handle_AuthoritativeSiegeKeepRequested(MessagePayload<AuthoritativeSiegeKeepRequested> payload)
    {
        var request = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(request.MapEventId, out var mapEvent)) return;

            if (TryOpenStage(mapEvent))
            {
                messageBroker.Publish(this, new SiegeKeepStageOpened(request.MapEventId));
                return;
            }

            // Nobody can lead the keep, so the walls end the way an AI-led assault does.
            Logger.Information("Keep stage for {MapEventId} has no attacking player to lead it; concluding the walls as an attacker victory",
                request.MapEventId);
            messageBroker.Publish(this, new AuthoritativeBattleConclusionRequested(
                request.MapEventId, BattleState.AttackerVictory, request.HostEpoch));
        }, context: nameof(Handle_AuthoritativeSiegeKeepRequested));
    }

    // [Server, game thread] Seats the walls players inside vanilla's hall limits and moves the siege into the hall.
    internal bool TryOpenStage(MapEvent mapEvent)
    {
        var settlement = mapEvent.MapEventSettlement;
        if (!mapEvent.IsSiegeAssault || mapEvent.IsFinalized || mapEvent.BattleState != BattleState.None ||
            settlement?.SiegeEvent == null || settlement.LocationComplex?.GetLocationWithId("lordshall") == null ||
            HasPlayerDefender(mapEvent))
            return false;

        var candidates = GetAttackingPlayers(mapEvent);
        if (candidates.Count == 0) return false;

        var model = Campaign.Current.Models.SiegeLordsHallFightModel;
        var defenders = model.GetPriorityListForLordsHallFightMission(mapEvent, BattleSideEnum.Defender,
            model.MaxDefenderSideTroopCount);
        var defenderBudget = new Dictionary<string, int>();
        foreach (var element in defenders)
        {
            if (!objectManager.TryGetId(element.Troop, out var characterId)) continue;
            defenderBudget.TryGetValue(characterId, out var count);
            defenderBudget[characterId] = count + 1;
        }
        var defenderCount = defenderBudget.Values.Sum();
        if (defenderCount == 0) return false;

        // Vanilla's hall picker limit, with every seated player's hero counted inside it.
        var attackerPlaces = Math.Max(1, Math.Min(model.MaxAttackerSideTroopCount,
            MathF.Round(defenderCount * model.AttackerDefenderTroopCountRatio)));
        var seated = candidates.Take(attackerPlaces).ToList();
        var shares = SiegeKeepTroopSelection.ShareEscortPlaces(attackerPlaces - seated.Count,
            seated.Select(candidate => Math.Max(0, candidate.Party.MemberRoster.TotalHealthyCount - 1)).ToList());
        var seats = seated.Select((candidate, index) => new SiegeKeepSeat(candidate.Player.ControllerId,
            candidate.Player.MobilePartyId, candidate.Player.CharacterObjectId, shares[index])).ToList();

        selection.Open(mapEvent, seats, defenderBudget, DateTime.UtcNow + SelectionWindow);
        // The walls reserves are spent; the keep builds fresh ones from the surviving rosters.
        reserveBuilder.ForgetMapEvent(mapEvent, preserveHealth: true);
        settlement.CurrentSiegeState = Settlement.SiegeState.InTheLordsHall;

        Logger.Information("Opened keep stage for {Settlement}: {Defenders} defenders, {Places} attacker places, {Seated} of {Players} players seated",
            settlement.StringId, defenderCount, attackerPlaces, seats.Count, candidates.Count);
        SendPhase(mapEvent, SiegeKeepPhase.Choosing);
        return true;
    }

    private bool HasPlayerDefender(MapEvent mapEvent) =>
        mapEvent.DefenderSide.Parties.Any(party =>
            party.Party?.MobileParty != null && playerManager.TryGetPlayer(party.Party.MobileParty, out _));

    // The siege leader first, then the other walls players in the attacker side's order.
    private List<(Player Player, MobileParty Party)> GetAttackingPlayers(MapEvent mapEvent)
    {
        var players = new List<(Player, MobileParty)>();
        var leader = mapEvent.AttackerSide.LeaderParty?.MobileParty;
        foreach (var mapEventParty in mapEvent.AttackerSide.Parties.OrderBy(party => party.Party?.MobileParty == leader ? 0 : 1))
        {
            var party = mapEventParty.Party?.MobileParty;
            if (party == null || !party.IsActive || !playerManager.TryGetPlayer(party, out var player) ||
                !playerManager.IsConnected(player) ||
                !objectManager.TryGetObject<Hero>(player.HeroId, out var hero) || hero.IsWounded)
                continue;

            players.Add((player, party));
        }
        return players;
    }

    private void Handle_EntryRequest(MessagePayload<NetworkSiegeKeepEntryRequest> payload)
    {
        if (payload.Who is not NetPeer peer) return;

        var request = payload.What;
        GameThread.RunSafe(() =>
        {
            NetworkSiegeKeepEntryReply reply;
            try
            {
                reply = Admit(peer, request);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Unable to answer the keep request for {MapEventId}", request.MapEventId);
                reply = Reject(request, "Unable to prepare the keep battle.");
            }
            network.Send(peer, reply);
        }, context: nameof(Handle_EntryRequest));
    }

    private NetworkSiegeKeepEntryReply Admit(NetPeer peer, NetworkSiegeKeepEntryRequest request)
    {
        if (!playerManager.TryGetPlayer(peer, out var player) ||
            !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party) ||
            !objectManager.TryGetObject<MapEvent>(request.MapEventId, out var mapEvent) ||
            !mapEvent.IsSiegeAssault || mapEvent.IsFinalized || mapEvent.BattleState != BattleState.None ||
            !ReferenceEquals(party.MapEvent, mapEvent))
            return Reject(request, "This siege assault is no longer open.");

        // A later assault on a siege that already reached the hall goes straight to the keep.
        if (!selection.IsOpen(mapEvent) &&
            (mapEvent.MapEventSettlement?.CurrentSiegeState != Settlement.SiegeState.InTheLordsHall ||
             !TryOpenStage(mapEvent)))
            return Reject(request, "Nobody can lead the keep battle right now.");

        if (!selection.TryGetSeat(mapEvent, player.MobilePartyId, out var seat, out var existing))
            return Reject(request, selection.IsLocked(mapEvent)
                ? "The keep battle has started without you. You will see the result."
                : "The keep has no place left for you. You will see the result.");

        var seconds = SecondsRemaining(mapEvent);
        if (selection.IsLocked(mapEvent))
            return new NetworkSiegeKeepEntryReply(request.RequestId, true, null, seat.EscortAllowance, 0, existing,
                isLocked: true);

        if (request.QueryOnly)
            return new NetworkSiegeKeepEntryReply(request.RequestId, true, null, seat.EscortAllowance, seconds,
                existing);

        if (!selection.TrySelect(mapEvent, party, request.Troops ?? Array.Empty<HideoutTroopSelectionEntry>(),
                out var accepted))
            return Reject(request, "That troop selection is no longer available.");

        if (selection.HaveAllAnswered(mapEvent))
            LockStage(mapEvent);

        return new NetworkSiegeKeepEntryReply(request.RequestId, true, null, seat.EscortAllowance, seconds, accepted,
            isLocked: selection.IsLocked(mapEvent));
    }

    private static NetworkSiegeKeepEntryReply Reject(NetworkSiegeKeepEntryRequest request, string reason) =>
        new(request.RequestId, false, reason, 0, 0, Array.Empty<HideoutTroopSelectionEntry>());

    private int SecondsRemaining(MapEvent mapEvent) =>
        selection.TryGetDeadline(mapEvent, out var deadline)
            ? Math.Max(0, (int)Math.Ceiling((deadline - DateTime.UtcNow).TotalSeconds))
            : 0;

    private void Handle_CampaignTick(MessagePayload<CampaignTick> payload)
    {
        if (ModInformation.IsClient) return;

        foreach (var mapEvent in selection.GetOpenMapEvents())
        {
            if (selection.IsLocked(mapEvent)) continue;

            // A player who left before the roster locked frees the wait for the others.
            foreach (var seat in selection.GetSeats(mapEvent))
                if (!IsConnected(seat.ControllerId))
                    selection.RemoveSeat(mapEvent, seat.MobilePartyId);

            if (selection.HaveAllAnswered(mapEvent) ||
                (selection.TryGetDeadline(mapEvent, out var deadline) && DateTime.UtcNow >= deadline))
                LockStage(mapEvent);
        }
    }

    // [Server, game thread] Gives a player who never answered vanilla's suggested squad, then fixes the roster.
    private void LockStage(MapEvent mapEvent)
    {
        foreach (var seat in selection.GetSeats(mapEvent))
        {
            if (!IsConnected(seat.ControllerId) ||
                !objectManager.TryGetObject<MobileParty>(seat.MobilePartyId, out var party) ||
                !ReferenceEquals(party.MapEvent, mapEvent))
            {
                selection.RemoveSeat(mapEvent, seat.MobilePartyId);
                continue;
            }

            if (selection.TryGetSeat(mapEvent, seat.MobilePartyId, out _, out var existing) && existing.Length > 0)
                continue;

            if (!selection.TrySelect(mapEvent, party, SuggestSquad(party, seat), out _))
                selection.RemoveSeat(mapEvent, seat.MobilePartyId);
        }

        if (selection.GetSeats(mapEvent).Count == 0)
        {
            Logger.Information("Closing keep stage for {MapEventId}: no seated player is left", mapEvent.StringId);
            SendPhase(mapEvent, SiegeKeepPhase.Closed);
            selection.Forget(mapEvent);
            return;
        }

        selection.Lock(mapEvent);
        Logger.Information("Locked keep roster for {MapEventId} with {Seats} player(s)",
            mapEvent.StringId, selection.GetSeats(mapEvent).Count);
        SendPhase(mapEvent, SiegeKeepPhase.Locked);
    }

    // The strongest healthy troops, companions first, as vanilla's hall picker suggests them.
    private HideoutTroopSelectionEntry[] SuggestSquad(MobileParty party, SiegeKeepSeat seat)
    {
        var roster = party.MemberRoster.ToFlattenedRoster();
        roster.RemoveIf(element => element.IsWounded ||
            (objectManager.TryGetId(element.Troop, out var characterId) && characterId == seat.HeroCharacterId));
        var squad = MobilePartyHelper.GetStrongestAndPriorTroops(roster, seat.EscortAllowance, includePlayer: false);
        var entries = new List<HideoutTroopSelectionEntry>();
        foreach (var element in squad.GetTroopRoster())
            if (element.Number > 0 && objectManager.TryGetId(element.Character, out var characterId))
                entries.Add(new HideoutTroopSelectionEntry(characterId, element.Number));
        return entries.ToArray();
    }

    private bool IsConnected(string controllerId) =>
        playerManager.TryGetPlayer(controllerId, out var player) && playerManager.IsConnected(player);

    // Every player in the map event hears where the keep stands, seated or not.
    private void SendPhase(MapEvent mapEvent, SiegeKeepPhase phase)
    {
        if (!objectManager.TryGetId(mapEvent, out var mapEventId)) return;

        var seconds = SecondsRemaining(mapEvent);
        foreach (var player in playerManager.Players)
        {
            if (!playerManager.TryGetPeer(player.ControllerId, out var peer) ||
                !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party) ||
                !ReferenceEquals(party.MapEvent, mapEvent))
                continue;

            var isSeated = selection.TryGetSeat(mapEvent, player.MobilePartyId, out var seat, out _);
            network.Send(peer, new NetworkSiegeKeepPhaseChanged(mapEventId, phase, isSeated,
                isSeated ? seat.EscortAllowance : 0, seconds));
        }
    }

    private void Handle_MapEventFinalized(MessagePayload<MapEventFinalized> payload) =>
        selection.Forget(payload.What.MapEvent);

    /// <summary>[Client] Opens the troop picker for the keep, called from the encounter's Attack option.</summary>
    internal void OpenTroopSelection(MenuCallbackArgs args)
    {
        var battle = PlayerEncounter.Battle ?? MobileParty.MainParty?.MapEvent;
        if (battle == null || !objectManager.TryGetIdWithLogging(battle, out var mapEventId)) return;
        if (!RequestBlocking(mapEventId, true, Array.Empty<HideoutTroopSelectionEntry>(), out var reply)) return;

        if (reply.IsLocked)
        {
            RequestKeepMission(mapEventId);
            return;
        }

        if (reply.EscortAllowance == 0)
        {
            var heroOnly = TroopRoster.CreateDummyTroopRoster();
            heroOnly.AddToCounts(Hero.MainHero.CharacterObject, 1);
            SubmitSelection(mapEventId, heroOnly);
            return;
        }

        var maximum = reply.EscortAllowance + 1;
        var selected = MobilePartyHelper.GetStrongestAndPriorTroops(MobileParty.MainParty, maximum, includePlayer: true);
        var menuContext = args?.MenuContext ?? Campaign.Current.CurrentMenuContext;
        if (menuContext == null) return;
        menuContext.OpenTroopSelection(MobileParty.MainParty.MemberRoster, selected,
            character => !character.IsPlayerCharacter,
            roster => SubmitSelection(mapEventId, roster), maximum, 1);
        ShowMessage($"Choose up to {reply.EscortAllowance} troops for the keep. The assault starts in {reply.SecondsRemaining} seconds.");
    }

    private void SubmitSelection(string mapEventId, TroopRoster selected)
    {
        if (selected == null) return;
        var troops = new List<HideoutTroopSelectionEntry>();
        foreach (var element in selected.GetTroopRoster())
        {
            if (element.Number <= 0) continue;
            if (!objectManager.TryGetIdWithLogging(element.Character, out var characterId)) return;
            troops.Add(new HideoutTroopSelectionEntry(characterId, element.Number));
        }

        if (!RequestBlocking(mapEventId, false, troops.ToArray(), out var reply)) return;
        if (!reply.IsLocked)
            ShowMessage("Your keep troops are ready. Waiting for the other players.");
    }

    private bool RequestBlocking(string mapEventId, bool queryOnly, HideoutTroopSelectionEntry[] troops,
        out NetworkSiegeKeepEntryReply reply)
    {
        reply = default;
        var requestId = Guid.NewGuid().ToString();
        var pending = new PendingEntry();
        pendingEntries[requestId] = pending;
        try
        {
            network.SendAll(new NetworkSiegeKeepEntryRequest(requestId, mapEventId, queryOnly, troops));
            if (!GameThread.WaitWhilePumping(() => pending.Completed.IsSet,
                    DateTime.UtcNow + configuration.ObjectCreationTimeout))
            {
                ShowMessage("The server has not answered the keep request. Try again.");
                return false;
            }
            reply = pending.Reply;
            if (!reply.Accepted) ShowMessage(reply.Reason ?? "Unable to join the keep battle.");
            return reply.Accepted;
        }
        finally
        {
            pendingEntries.TryRemove(requestId, out _);
        }
    }

    private void Handle_EntryReply(MessagePayload<NetworkSiegeKeepEntryReply> payload)
    {
        if (!pendingEntries.TryGetValue(payload.What.RequestId, out var pending)) return;
        pending.Reply = payload.What;
        pending.Completed.Set();
    }

    private void Handle_PhaseChanged(MessagePayload<NetworkSiegeKeepPhaseChanged> payload)
    {
        var message = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObject<MapEvent>(message.MapEventId, out var mapEvent) ||
                !ReferenceEquals(MobileParty.MainParty?.MapEvent, mapEvent))
                return;

            switch (message.Phase)
            {
                case SiegeKeepPhase.Choosing:
                    // The walls mission left its local pull-back (or a lagging victory) here; the server's event stays unresolved.
                    if (mapEvent.BattleState != BattleState.None)
                        using (new AllowedThread())
                            mapEvent.ResetBattleState();
                    ShowMessage(message.IsSeated
                        ? $"Walls taken. The defenders fell back to the keep. Attack to choose up to {message.EscortAllowance} troops within {message.SecondsRemaining} seconds."
                        : "Walls taken. The defenders fell back to the keep. The keep has no place left for you, you will see the result.");
                    break;
                case SiegeKeepPhase.Locked:
                    if (!message.IsSeated)
                    {
                        ShowMessage("The keep battle has started. You will see the result.");
                        break;
                    }
                    ShowMessage("The keep assault is starting.");
                    RequestKeepMission(message.MapEventId);
                    break;
                case SiegeKeepPhase.Closed:
                    ShowMessage("Nobody is left to lead the keep assault.");
                    break;
            }
        }, context: nameof(Handle_PhaseChanged));
    }

    // The server answers through the normal mission start, which opens the keep for every seated player.
    private void RequestKeepMission(string mapEventId)
    {
        if (!objectManager.TryGetIdWithLogging(MobileParty.MainParty, out var partyId)) return;
        network.SendAll(new NetworkBattleStartRequest(Guid.NewGuid().ToString(), (int)BattleStartMode.Mission,
            mapEventId, partyId));
    }

    private static void ShowMessage(string text) => InformationManager.DisplayMessage(new InformationMessage(text));

    private sealed class PendingEntry
    {
        public ManualResetEventSlim Completed { get; } = new(false);
        public NetworkSiegeKeepEntryReply Reply;
    }
}
