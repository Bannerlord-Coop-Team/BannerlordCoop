using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Barters;
using GameInterface.Services.Hideouts.Messages;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Helpers;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Hideouts.Handlers;

internal sealed class HideoutRaidHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<HideoutRaidHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly INetworkConfig configuration;
    private readonly IHideoutTroopSelection troopSelection;
    private readonly IHideoutPreparation preparation;
    private readonly HideoutCampaignConsequencesHandler consequences;
    private readonly Dictionary<MapEvent, HideoutRaidState> raids = new();
    private readonly ConcurrentDictionary<string, PendingEntry> pendingEntries = new();

    public HideoutRaidHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager,
        IPlayerManager playerManager, INetworkConfig configuration, IHideoutTroopSelection troopSelection,
        HideoutCampaignConsequencesHandler consequences, IHideoutPreparation preparation)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.configuration = configuration;
        this.troopSelection = troopSelection;
        this.consequences = consequences;
        this.preparation = preparation;
        messageBroker.Subscribe<NetworkHideoutRaidEntryRequest>(HandleRequest);
        messageBroker.Subscribe<NetworkHideoutRaidEntryReply>(HandleReply);
        messageBroker.Subscribe<MapEventFinalized>(HandleFinalized);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkHideoutRaidEntryRequest>(HandleRequest);
        messageBroker.Unsubscribe<NetworkHideoutRaidEntryReply>(HandleReply);
        messageBroker.Unsubscribe<MapEventFinalized>(HandleFinalized);
    }

    internal bool TryGetRaid(MapEvent mapEvent, out HideoutRaidState raid)
    {
        raid = null;
        return mapEvent != null && raids.TryGetValue(mapEvent, out raid);
    }

    internal bool IsAdmitted(MapEvent mapEvent, MobileParty party) =>
        TryGetRaid(mapEvent, out _) && troopSelection.HasSelection(mapEvent.MapEventSettlement, party);

    private void HandleFinalized(MessagePayload<MapEventFinalized> payload) => raids.Remove(payload.What.MapEvent);

    internal void OpenTroopSelection(MenuCallbackArgs args, bool isDirectAssault)
    {
        var settlement = Settlement.CurrentSettlement ?? PlayerEncounter.EncounteredBattle?.MapEventSettlement;
        if (settlement?.IsHideout != true) return;
        if (MobileParty.MainParty.CurrentSettlement != settlement)
        {
            if (MobileParty.MainParty.MapEvent != null || PlayerEncounter.Battle != null) return;
            if (PlayerEncounter.Current != null)
            {
                PlayerEncounter.LeaveEncounter = true;
                PlayerEncounter.Finish(forcePlayerOutFromSettlement: false);
            }
            EncounterManager.StartSettlementEncounter(MobileParty.MainParty, settlement);
            return;
        }
        if (!RequestBlocking(settlement, isDirectAssault, true, Array.Empty<HideoutTroopSelectionEntry>(), out var reply))
            return;

        var behavior = Campaign.Current.GetCampaignBehavior<HideoutCampaignBehavior>();
        if (behavior == null) return;
        if (reply.IsAdmitted || (reply.IsJoining && reply.RemainingTroops == 0))
        {
            var heroOnly = TroopRoster.CreateDummyTroopRoster();
            heroOnly.AddToCounts(Hero.MainHero.CharacterObject, 1);
            StartRaid(behavior, heroOnly, reply.IsDirectAssault);
            return;
        }

        var selected = MobilePartyHelper.GetStrongestAndPriorTroops(
            MobileParty.MainParty, reply.RemainingTroops + 1, includePlayer: true);
        var minimum = reply.IsJoining ? 1 : Campaign.Current.Models.BanditDensityModel
            .GetMinimumTroopCountForHideoutMission(MobileParty.MainParty, reply.IsDirectAssault);
        args.MenuContext.OpenTroopSelection(MobileParty.MainParty.MemberRoster, selected,
            behavior.CanChangeStatusOfTroop,
            roster => StartRaid(behavior, roster, reply.IsDirectAssault), reply.RemainingTroops + 1, minimum);
    }

    internal void StartRaid(HideoutCampaignBehavior behavior, TroopRoster selected, bool isDirectAssault)
    {
        if (selected == null) return;
        var troops = new List<HideoutTroopSelectionEntry>();
        foreach (var element in selected.GetTroopRoster())
        {
            if (element.Number <= 0) continue;
            if (!objectManager.TryGetIdWithLogging(element.Character, out var characterId)) return;
            troops.Add(new HideoutTroopSelectionEntry(characterId, element.Number));
        }

        if (!RequestBlocking(Settlement.CurrentSettlement, isDirectAssault, false, troops.ToArray(), out var reply))
            return;

        MapEvent mapEvent = null;
        if (!GameThread.WaitWhilePumping(() =>
            objectManager.TryGetObject(reply.MapEventId, out mapEvent) &&
            ReferenceEquals(MobileParty.MainParty?.MapEvent, mapEvent) &&
            Campaign.Current.MapEventManager.MapEvents.Contains(mapEvent),
            DateTime.UtcNow + configuration.ObjectCreationTimeout))
        {
            ShowFailure("The hideout battle has not finished synchronizing. Try joining again.");
            return;
        }

        BattleMissionStartHandler.InitializePlayerEncounter(mapEvent);
        behavior.UpdateInitialHideoutPopulation();
        GameMenu.SwitchToMenu("hideout_place");
        if (!ContainerProvider.TryResolve<BattleStartCoordinator>(out var coordinator) ||
            !objectManager.TryGetIdWithLogging(MobileParty.MainParty, out var partyId) ||
            !coordinator.RequestBlocking(BattleStartMode.Mission, reply.MapEventId, partyId))
            ShowFailure("Unable to open the hideout mission. You can try joining the attack again.");
    }

    private bool RequestBlocking(Settlement settlement, bool isDirectAssault, bool queryOnly,
        HideoutTroopSelectionEntry[] troops, out NetworkHideoutRaidEntryReply reply)
    {
        reply = default;
        if (ModInformation.IsServer || settlement?.IsHideout != true ||
            !objectManager.TryGetIdWithLogging(settlement, out var settlementId)) return false;
        var requestId = Guid.NewGuid().ToString();
        var pending = new PendingEntry();
        pendingEntries[requestId] = pending;
        try
        {
            network.SendAll(new NetworkHideoutRaidEntryRequest(requestId, settlementId, isDirectAssault, queryOnly, troops));
            if (!GameThread.WaitWhilePumping(() => pending.Completed.IsSet,
                    DateTime.UtcNow + configuration.ObjectCreationTimeout))
            {
                ShowFailure("The server has not answered the hideout request. Try again.");
                return false;
            }
            reply = pending.Reply;
            if (!reply.Accepted) ShowFailure(reply.Reason ?? "Unable to join this hideout attack.");
            return reply.Accepted;
        }
        finally
        {
            pendingEntries.TryRemove(requestId, out _);
        }
    }

    private static void ShowFailure(string reason) => InformationManager.DisplayMessage(new InformationMessage(reason));

    private void HandleReply(MessagePayload<NetworkHideoutRaidEntryReply> payload)
    {
        if (ModInformation.IsServer || !pendingEntries.TryGetValue(payload.What.RequestId, out var pending)) return;
        pending.Reply = payload.What;
        pending.Completed.Set();
    }

    private void HandleRequest(MessagePayload<NetworkHideoutRaidEntryRequest> payload)
    {
        if (ModInformation.IsClient || payload.Who is not NetPeer peer) return;
        GameThread.RunSafe(() =>
        {
            var request = payload.What;
            NetworkHideoutRaidEntryReply reply;
            try
            {
                reply = Admit(peer, request);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Unable to admit player to hideout {SettlementId}", request.SettlementId);
                reply = Reject(request, "Unable to prepare this hideout attack.");
            }
            network.Send(peer, reply);
        }, context: nameof(NetworkHideoutRaidEntryRequest));
    }

    internal NetworkHideoutRaidEntryReply Admit(NetPeer peer, NetworkHideoutRaidEntryRequest request)
    {
        if (!playerManager.TryGetPlayer(peer, out var player) ||
            !objectManager.TryGetObject<Hero>(player.HeroId, out var hero) ||
            !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party) ||
            !objectManager.TryGetObject<Settlement>(request.SettlementId, out var settlement) ||
            settlement?.IsHideout != true || party.IsActive != true ||
            party.CurrentSettlement != settlement || hero.IsWounded)
            return Reject(request, "You must be inside the hideout with a healthy hero.");

        var mapEvent = settlement.Party.MapEvent;
        var joining = TryGetRaid(mapEvent, out var raid);
        if (mapEvent != null && (!joining || mapEvent.IsFinalized || mapEvent.BattleState != BattleState.None))
            return Reject(request, "This hideout attack is no longer open.");
        if (party.MapEvent != null && !ReferenceEquals(party.MapEvent, mapEvent))
            return Reject(request, "Your party is already in another battle.");
        if (party.Party.MapEventSide != null && !ReferenceEquals(party.Party.MapEventSide, mapEvent?.AttackerSide))
            return Reject(request, "Your party is already fighting against this attack.");
        if (!joining && (!settlement.Hideout.IsInfested || !settlement.Hideout.NextPossibleAttackTime.IsPast))
            return Reject(request, "This hideout cannot be attacked yet.");
        if (!joining && preparation.GetState(settlement, party) == HideoutEntryState.Waiting)
            return Reject(request, $"Waiting for {preparation.GetAttackLeader(settlement)?.Name} to set up their attack.");

        var direct = joining ? raid.IsDirectAssault : request.IsDirectAssault;
        var limit = joining ? raid.EscortLimit : Math.Max(0, Campaign.Current.Models.BanditDensityModel
            .GetMaximumTroopCountForHideoutMission(party, direct) - 1);
        var remaining = troopSelection.GetRemaining(settlement, limit);
        if (request.QueryOnly)
            return new NetworkHideoutRaidEntryReply(request.RequestId, true, null, remaining, direct, joining,
                isAdmitted: troopSelection.HasSelection(settlement, party));

        if (!joining)
        {
            var behavior = Campaign.Current.GetCampaignBehavior<HideoutCampaignBehavior>();
            if (behavior == null || behavior.IsItNighttimeNow() == direct)
                return Reject(request, "The time of day no longer allows this hideout attack.");
        }
        if (!troopSelection.TrySelect(settlement, party, player.ControllerId, limit, request.Troops,
                out var accepted, out remaining))
            return Reject(request, $"The troop selection is no longer available. {remaining} shared troop slots remain.");

        var bound = joining;
        try
        {
            if (!joining)
            {
                var minimum = Campaign.Current.Models.BanditDensityModel.GetMinimumTroopCountForHideoutMission(party, direct);
                if (accepted.Sum(entry => entry.Count) < minimum)
                    return Reject(request, "Select enough troops to start the hideout attack.");
                using var context = new BarterPlayerContext(hero, party);
                if (!consequences.TryPrepareRaid(settlement, direct))
                    return Reject(request, "Unable to prepare the hideout defenders.");
                mapEvent = HideoutEventComponent.CreateHideoutEvent(party.Party, settlement.Party, false).MapEvent;
                if (mapEvent == null || !objectManager.TryGetId(mapEvent, out _) ||
                    !Campaign.Current.MapEventManager.MapEvents.Contains(mapEvent) ||
                    !ReferenceEquals(party.Party.MapEventSide, mapEvent.AttackerSide))
                    return Reject(request, "Unable to attach your party to the hideout attack.");
                raid = new HideoutRaidState(direct, limit, player.MobilePartyId);
                raids.Add(mapEvent, raid);
                troopSelection.BindMapEvent(settlement, mapEvent);
                bound = true;
                settlement.Hideout.SetNextPossibleAttackTime(Campaign.Current.Models.HideoutModel.HideoutHiddenDuration);
            }
            else if (party.Party.MapEventSide == null)
            {
                using var context = new BarterPlayerContext(hero, party);
                party.Party.MapEventSide = mapEvent.AttackerSide;
            }
            if (!ReferenceEquals(party.Party.MapEventSide, mapEvent.AttackerSide) ||
                !objectManager.TryGetIdWithLogging(mapEvent, out var mapEventId))
                return Reject(request, "Unable to attach your party to the hideout attack.");
            return new NetworkHideoutRaidEntryReply(request.RequestId, true, mapEventId, remaining, direct, joining);
        }
        finally
        {
            if (!bound) troopSelection.CancelUnbound(settlement, party);
        }
    }

    private static NetworkHideoutRaidEntryReply Reject(NetworkHideoutRaidEntryRequest request, string reason) =>
        new(request.RequestId, false, null, 0, request.IsDirectAssault, false, reason);

    private sealed class PendingEntry
    {
        public ManualResetEventSlim Completed { get; } = new(false);
        public NetworkHideoutRaidEntryReply Reply;
    }
}

internal sealed class HideoutRaidState
{
    public bool IsDirectAssault { get; }
    public int EscortLimit { get; }
    public string InitiatingPartyId { get; }

    public HideoutRaidState(bool isDirectAssault, int escortLimit, string initiatingPartyId)
    {
        IsDirectAssault = isDirectAssault;
        EscortLimit = escortLimit;
        InitiatingPartyId = initiatingPartyId;
    }
}
