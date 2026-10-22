using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.SiegeEvents.Messages;
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
    private readonly ISendCoalescer coalescer;
    private readonly IBreakInReceipts receipts;

    public ServerSiegeBreakInHandler(IMessageBroker messageBroker, INetwork network,
        IObjectManager objectManager, IPlayerManager playerManager, ISiegeBreakOut breakOut,
        IBreakInReceipts receipts, ISendCoalescer coalescer = null)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.breakOut = breakOut;
        this.receipts = receipts;
        this.coalescer = coalescer;
        messageBroker.Subscribe<NetworkRequestBreakIn>(Handle);
    }

    private void Handle(MessagePayload<NetworkRequestBreakIn> payload)
    {
        if (!(payload.Who is NetPeer peer)) return;
        var request = payload.What;
        GameThread.RunSafe(() => network.Send(peer, Apply(peer, request)), context: nameof(NetworkRequestBreakIn));
    }

    private NetworkBreakInResult Apply(NetPeer peer, NetworkRequestBreakIn request)
    {
        var rejected = new NetworkBreakInResult(request.RequestId, false, Array.Empty<uint>(), Array.Empty<int>(), -1);
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

        // Asking again for the same siege, even after leaving the menu, gets the first answer instead of losing troops twice.
        if (receipts.TryGet(party, siege, out var receipt))
            return new NetworkBreakInResult(request.RequestId, receipt.Result.Approved,
                receipt.Result.CharacterIds, receipt.Result.Counts, receipt.Result.ArmyCasualties);

        if (!breakOut.CanJoinDefenders(party, settlement, alreadyEntered: false))
        {
            Logger.Warning("Rejecting break in request {RequestId} for party {PartyId} and settlement {SettlementId}",
                request.RequestId, request.PartyId, request.SettlementId);
            return rejected;
        }

        try
        {
            int losses = breakOut.GetBreakInCasualties(party, siege);
            var paidReceipt = new BreakInReceipts.Receipt { Siege = siege, Result = rejected };
            var paid = SiegeSacrifice.TryApply(objectManager, breakOut, network, coalescer, party, losses,
                () => receipts.Set(party, paidReceipt));
            if (paid == null) return rejected;
            paidReceipt.Result = new NetworkBreakInResult(request.RequestId, true, paid.CharacterIds, paid.Counts, paid.ArmyCasualties);
            return paidReceipt.Result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to apply break in request {RequestId}", request.RequestId);
            return rejected;
        }
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestBreakIn>(Handle);
        receipts.Clear();
    }
}
