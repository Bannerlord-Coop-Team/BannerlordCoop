using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.Settlements;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.SiegeEvents;
using LiteNetLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace Coop.Core.Server.Services.SiegeEvents.Handlers;

/// <summary>
/// Applies the troops a player loses breaking into a besieged settlement, so the normal roster sync
/// carries the same losses to every client instead of only the requester losing them locally.
/// </summary>
internal class ServerSiegeBreakInHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerSiegeBreakInHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ISiegeBreakOut breakOut;
    private readonly ISiegeSacrifice sacrifice;
    private readonly IBreakInReceipts receipts;
    private readonly ISettlementEncounterDistanceValidator distanceValidator;

    public ServerSiegeBreakInHandler(IMessageBroker messageBroker, INetwork network,
        IObjectManager objectManager, IPlayerManager playerManager, ISiegeBreakOut breakOut,
        ISiegeSacrifice sacrifice, IBreakInReceipts receipts, ISettlementEncounterDistanceValidator distanceValidator)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.breakOut = breakOut;
        this.sacrifice = sacrifice;
        this.receipts = receipts;
        this.distanceValidator = distanceValidator;
        messageBroker.Subscribe<NetworkRequestBreakIn>(Handle);
        messageBroker.Subscribe<PartyEnterSettlementAttempted>(HandleEnter);
        messageBroker.Subscribe<SettlementEncounterLeaveApplied>(HandleLeave);
        messageBroker.Subscribe<PlayerDisconnected>(HandleDisconnect);
    }

    private void Handle(MessagePayload<NetworkRequestBreakIn> payload)
    {
        if (!(payload.Who is NetPeer peer)) return;
        var request = payload.What;
        GameThread.RunSafe(() => network.Send(peer, Answer(peer, request)), context: nameof(NetworkRequestBreakIn));
    }

    // Every failure still answers, so the client's Reject lock always clears.
    private NetworkBreakInResult Answer(NetPeer peer, NetworkRequestBreakIn request)
    {
        var rejected = new NetworkBreakInResult(request.RequestId, false, Array.Empty<uint>(), Array.Empty<int>(), -1);
        try
        {
            return Apply(peer, request, rejected);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to apply break in request {RequestId}", request.RequestId);
            return rejected;
        }
    }

    private NetworkBreakInResult Apply(NetPeer peer, NetworkRequestBreakIn request, NetworkBreakInResult rejected)
    {
        if (!playerManager.TryGetPlayer(peer, out var player) ||
            !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var controlled) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(request.PartyId, out var party) ||
            !ReferenceEquals(controlled, party))
        {
            Logger.Warning("Rejecting break in request {RequestId} from a peer that does not control party {PartyId}",
                request.RequestId, request.PartyId);
            return rejected;
        }

        if (!objectManager.TryGetObjectWithLogging<Settlement>(request.SettlementId, out var settlement) ||
            !objectManager.TryGetObjectWithLogging<SiegeEvent>(request.SiegeId, out var siege) ||
            settlement.SiegeEvent != siege)
        {
            Logger.Warning("Rejecting break in request {RequestId} for party {PartyId} and settlement {SettlementId}",
                request.RequestId, request.PartyId, request.SettlementId);
            return rejected;
        }

        // Vanilla only offers the break in to a party at the settlement that can still join the defenders. Checked before
        // the receipt too, so a request that lands after the siege moved on or the party was moved is turned down.
        string reason = "the party cannot join the defenders";
        if (!breakOut.CanJoinDefenders(party, settlement, alreadyEntered: false) ||
            !distanceValidator.TryValidate(party, settlement, out reason))
        {
            Logger.Warning("Rejecting break in request {RequestId} for party {PartyId} and settlement {SettlementId}: {Reason}",
                request.RequestId, request.PartyId, request.SettlementId, reason);
            return rejected;
        }

        // Asking again for the same siege, even after leaving the menu, gets the first answer instead of losing troops twice.
        if (receipts.TryGet(party, siege, out var receipt))
            return new NetworkBreakInResult(request.RequestId, receipt.Result.Approved,
                receipt.Result.CharacterIds, receipt.Result.Counts, receipt.Result.ArmyCasualties);

        int losses = breakOut.GetBreakInCasualties(party, siege);
        var paidReceipt = new BreakInReceipts.Receipt { Siege = siege, Peer = peer, Result = rejected };
        var paid = sacrifice.TryApply(party, losses, () => receipts.Set(party, paidReceipt));
        if (paid == null) return rejected;
        paidReceipt.Result = new NetworkBreakInResult(request.RequestId, true, paid.CharacterIds, paid.Counts, paid.ArmyCasualties);
        return paidReceipt.Result;
    }

    // Dropped on enter, leave and disconnect, so a paid or failed break in never outlives the encounter it was for.
    // Break out keeps its own receipts in ServerSiegeBreakOutHandler, which always drop on enter and have no disconnect cleanup.
    private void HandleEnter(MessagePayload<PartyEnterSettlementAttempted> payload)
    {
        var party = payload.What.MobileParty;
        // Entering the paid siege is the continuation itself, which drops the receipt once the party is really inside.
        if (receipts.TryGet(party, payload.What.Settlement?.SiegeEvent, out _)) return;
        receipts.Remove(party);
    }

    private void HandleLeave(MessagePayload<SettlementEncounterLeaveApplied> payload)
    {
        receipts.Remove(payload.What.Party);
    }

    private void HandleDisconnect(MessagePayload<PlayerDisconnected> payload)
    {
        var peer = payload.What.PlayerId;
        GameThread.RunSafe(() => receipts.RemoveAll(peer), context: nameof(PlayerDisconnected));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestBreakIn>(Handle);
        messageBroker.Unsubscribe<PartyEnterSettlementAttempted>(HandleEnter);
        messageBroker.Unsubscribe<SettlementEncounterLeaveApplied>(HandleLeave);
        messageBroker.Unsubscribe<PlayerDisconnected>(HandleDisconnect);
        receipts.Clear();
    }
}
