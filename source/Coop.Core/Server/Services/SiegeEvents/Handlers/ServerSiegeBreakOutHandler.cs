using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.SiegeEvents;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace Coop.Core.Server.Services.SiegeEvents.Handlers;

internal class ServerSiegeBreakOutHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerSiegeBreakOutHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ISiegeBreakOut breakOut;
    private readonly ISiegeSacrifice sacrifice;
    private readonly Dictionary<MobileParty, Receipt> receipts = new();

    private sealed class Receipt
    {
        public SiegeEvent Siege;
        public bool IsFromPort;
        public NetworkBreakOutResult Result;
    }

    public ServerSiegeBreakOutHandler(IMessageBroker messageBroker, INetwork network,
        IObjectManager objectManager, IPlayerManager playerManager, ISiegeBreakOut breakOut,
        ISiegeSacrifice sacrifice)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.breakOut = breakOut;
        this.sacrifice = sacrifice;
        messageBroker.Subscribe<NetworkRequestBreakOut>(Handle);
        messageBroker.Subscribe<PartyEnterSettlementAttempted>(HandleEnter);
        messageBroker.Subscribe<SettlementEncounterLeaveApplied>(HandleLeave);
    }

    private void Handle(MessagePayload<NetworkRequestBreakOut> payload)
    {
        if (!(payload.Who is NetPeer peer)) return;
        var request = payload.What;
        GameThread.RunSafe(() => network.Send(peer, Answer(peer, request)), context: nameof(NetworkRequestBreakOut));
    }

    // Every failure still answers, so the client never waits on a reply that is not coming.
    private NetworkBreakOutResult Answer(NetPeer peer, NetworkRequestBreakOut request)
    {
        var rejected = new NetworkBreakOutResult(request.RequestId, false, Array.Empty<uint>(), Array.Empty<int>(), -1);
        try
        {
            return Apply(peer, request, rejected);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to apply breakout request {RequestId}", request.RequestId);
            return rejected;
        }
    }

    private NetworkBreakOutResult Apply(NetPeer peer, NetworkRequestBreakOut request, NetworkBreakOutResult rejected)
    {
        if (!playerManager.TryGetPlayer(peer, out var player) ||
            !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var controlled) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(request.PartyId, out var party) ||
            !ReferenceEquals(controlled, party))
        {
            Logger.Warning("Rejecting breakout request {RequestId} from a peer that does not control party {PartyId}",
                request.RequestId, request.PartyId);
            return rejected;
        }

        if (!objectManager.TryGetObjectWithLogging<Settlement>(request.SettlementId, out var settlement) ||
            !objectManager.TryGetObjectWithLogging<SiegeEvent>(request.SiegeId, out var siege) ||
            settlement.SiegeEvent != siege)
        {
            Logger.Warning("Rejecting breakout request {RequestId} for party {PartyId} and settlement {SettlementId}",
                request.RequestId, request.PartyId, request.SettlementId);
            return rejected;
        }

        if (!party.IsActive || party.CurrentSettlement != settlement || party.BesiegerCamp != null ||
            party.MapEvent != null || settlement.Party?.MapEvent != null)
        {
            Logger.Warning("Rejecting breakout request {RequestId} for party {PartyId} that cannot leave settlement {SettlementId}",
                request.RequestId, request.PartyId, request.SettlementId);
            return rejected;
        }

        // The client flag alone never opens the port; vanilla needs a port, ships and the fleet anchored there.
        if (request.IsFromPort && !breakOut.CanBreakOutFromPort(party, settlement))
        {
            Logger.Warning("Rejecting port breakout request {RequestId} for party {PartyId} that cannot use the port of {SettlementId}",
                request.RequestId, request.PartyId, request.SettlementId);
            return rejected;
        }

        if (receipts.TryGetValue(party, out var receipt) && receipt.Siege == siege)
        {
            // A paid gate break out is not reused for the port or the other way round, so this request is turned down.
            if (receipt.IsFromPort != request.IsFromPort) return rejected;
            return new NetworkBreakOutResult(request.RequestId, receipt.Result.Approved,
                receipt.Result.CharacterIds, receipt.Result.Counts, receipt.Result.ArmyCasualties);
        }

        int losses = breakOut.GetRequiredCasualties(party, request.IsFromPort);
        var paidReceipt = new Receipt { Siege = siege, IsFromPort = request.IsFromPort, Result = rejected };
        var paid = sacrifice.TryApply(party, losses, () => receipts[party] = paidReceipt);
        if (paid == null) return rejected;
        paidReceipt.Result = new NetworkBreakOutResult(request.RequestId, true, paid.CharacterIds, paid.Counts, paid.ArmyCasualties);
        return paidReceipt.Result;
    }

    private void HandleEnter(MessagePayload<PartyEnterSettlementAttempted> payload)
    {
        receipts.Remove(payload.What.MobileParty);
    }

    private void HandleLeave(MessagePayload<SettlementEncounterLeaveApplied> payload)
    {
        var party = payload.What.Party;
        if (!receipts.TryGetValue(party, out var receipt)) return;
        receipts.Remove(party);
        // A port break out sails away from the port, so it keeps the position vanilla gives it.
        if (!receipt.Result.Approved || receipt.IsFromPort || party.CurrentSettlement != null ||
            payload.What.Settlement != receipt.Siege.BesiegedSettlement) return;

        breakOut.ProtectAfterLeave(party, payload.What.Settlement);
        if (!objectManager.TryGetIdWithLogging(party, out var partyId)) return;
        // The leave reply closes the requester's menu before this ordered position correction.
        network.SendAll(new NetworkSnapSiegeCampPartyPosition(partyId, party.Position));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestBreakOut>(Handle);
        messageBroker.Unsubscribe<PartyEnterSettlementAttempted>(HandleEnter);
        messageBroker.Unsubscribe<SettlementEncounterLeaveApplied>(HandleLeave);
        receipts.Clear();
    }
}
