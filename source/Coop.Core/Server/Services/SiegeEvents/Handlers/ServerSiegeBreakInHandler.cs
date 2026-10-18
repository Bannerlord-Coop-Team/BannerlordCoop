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
using System.Collections.Generic;
using System.Linq;
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
    private readonly Dictionary<MobileParty, Receipt> receipts = new();

    private sealed class Receipt
    {
        public string RequestId;
        public NetworkBreakInResult Result;
    }

    public ServerSiegeBreakInHandler(IMessageBroker messageBroker, INetwork network,
        IObjectManager objectManager, IPlayerManager playerManager, ISiegeBreakOut breakOut,
        ISendCoalescer coalescer = null)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.breakOut = breakOut;
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

        // A resent request gets the first answer instead of losing the troops twice.
        if (receipts.TryGetValue(party, out var receipt) && receipt.RequestId == request.RequestId)
            return receipt.Result;

        if (!objectManager.TryGetObjectWithLogging<Settlement>(request.SettlementId, out var settlement) ||
            !objectManager.TryGetObjectWithLogging<SiegeEvent>(request.SiegeId, out var siege) ||
            settlement.SiegeEvent != siege ||
            !breakOut.CanJoinDefenders(party, settlement, alreadyEntered: false))
        {
            Logger.Warning("Rejecting break in request {RequestId} for party {PartyId} and settlement {SettlementId}",
                request.RequestId, request.PartyId, request.SettlementId);
            return rejected;
        }

        var parties = party.Army?.LeaderParty == party ? party.Army.Parties.ToArray() : new[] { party };
        if (!AreRostersRegistered(parties)) return rejected;

        try
        {
            int losses = breakOut.GetBreakInCasualties(party, siege);
            // Cache before mutating so a failure part way through is not retried as a second sacrifice.
            receipt = new Receipt { RequestId = request.RequestId, Result = rejected };
            receipts[party] = receipt;
            var casualties = breakOut.ApplySacrifice(party, losses, out var armyCasualties);
            var elements = casualties.GetTroopRoster();
            var ids = new uint[elements.Count];
            var counts = new int[elements.Count];
            for (int index = 0; index < elements.Count; index++)
            {
                objectManager.TryGetHandleWithLogging(elements[index].Character, out ids[index]);
                counts[index] = elements[index].Number;
            }
            foreach (var member in parties)
                if (objectManager.TryGetHandle(member.MemberRoster, out var rosterId))
                    coalescer?.FlushInstance(rosterId, network);
            receipt.Result = new NetworkBreakInResult(request.RequestId, true, ids, counts, armyCasualties);
            return receipt.Result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to apply break in request {RequestId}", request.RequestId);
            return rejected;
        }
    }

    private bool AreRostersRegistered(MobileParty[] parties)
    {
        foreach (var member in parties)
        {
            if (!objectManager.TryGetHandleWithLogging(member.MemberRoster, out _)) return false;
            foreach (var element in member.MemberRoster.GetTroopRoster())
                if (element.Character.IsRegular && !objectManager.TryGetHandleWithLogging(element.Character, out _))
                    return false;
        }
        return true;
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestBreakIn>(Handle);
        receipts.Clear();
    }
}
