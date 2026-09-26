using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
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
using System.Linq;
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
    private readonly ISendCoalescer coalescer;
    private readonly Dictionary<MobileParty, Receipt> receipts = new();

    private sealed class Receipt
    {
        public SiegeEvent Siege;
        public NetworkBreakOutResult Result;
    }

    public ServerSiegeBreakOutHandler(IMessageBroker messageBroker, INetwork network,
        IObjectManager objectManager, IPlayerManager playerManager, ISiegeBreakOut breakOut,
        ISendCoalescer coalescer = null)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.breakOut = breakOut;
        this.coalescer = coalescer;
        messageBroker.Subscribe<NetworkRequestBreakOut>(Handle);
        messageBroker.Subscribe<PartyEnterSettlementAttempted>(HandleEnter);
        messageBroker.Subscribe<SettlementEncounterLeaveApplied>(HandleLeave);
    }

    private void Handle(MessagePayload<NetworkRequestBreakOut> payload)
    {
        if (!(payload.Who is NetPeer peer)) return;
        var request = payload.What;
        GameThread.RunSafe(() =>
        {
            var result = new NetworkBreakOutResult(request.RequestId, false, Array.Empty<uint>(), Array.Empty<int>(), -1);
            if (playerManager.TryGetPlayer(peer, out var player) &&
                objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var controlled) &&
                objectManager.TryGetObjectWithLogging<MobileParty>(request.PartyId, out var party) &&
                ReferenceEquals(controlled, party) &&
                objectManager.TryGetObjectWithLogging<Settlement>(request.SettlementId, out var settlement) &&
                objectManager.TryGetObjectWithLogging<SiegeEvent>(request.SiegeId, out var siege) &&
                party.IsActive && party.CurrentSettlement == settlement && settlement.SiegeEvent == siege &&
                party.BesiegerCamp == null && party.MapEvent == null && settlement.Party?.MapEvent == null)
            {
                if (receipts.TryGetValue(party, out var receipt) && receipt.Siege == siege)
                {
                    result = receipt.Result;
                    result = new NetworkBreakOutResult(request.RequestId, result.Approved,
                        result.CharacterIds, result.Counts, result.ArmyCasualties);
                }
                else
                {
                    try
                    {
                        var parties = party.Army?.LeaderParty == party ? party.Army.Parties.ToArray() : new[] { party };
                        foreach (var member in parties)
                        {
                            if (!objectManager.TryGetHandleWithLogging(member.MemberRoster, out _))
                                throw new InvalidOperationException("Breakout roster is not registered");
                            foreach (var element in member.MemberRoster.GetTroopRoster())
                                if (element.Character.IsRegular && !objectManager.TryGetHandleWithLogging(element.Character, out _))
                                    throw new InvalidOperationException("Breakout troop is not registered");
                        }
                        // Cache only once mutation can begin; missing registrations may recover on retry.
                        receipt = new Receipt { Siege = siege, Result = result };
                        receipts[party] = receipt;
                        var casualties = breakOut.ApplySacrifice(party, out var armyCasualties);
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
                        result = new NetworkBreakOutResult(request.RequestId, true, ids, counts, armyCasualties);
                        receipt.Result = result;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Failed to apply breakout request {RequestId}", request.RequestId);
                    }
                }
            }
            network.Send(peer, result);
        }, context: nameof(NetworkRequestBreakOut));
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
        if (!receipt.Result.Approved || party.CurrentSettlement != null ||
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
