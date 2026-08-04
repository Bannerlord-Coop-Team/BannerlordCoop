using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Core.Client.Services.BattleRetreat.Messages;
using Coop.Core.Server.Services.BattleRetreat.Messages;
using GameInterface.Services.MapEvents.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Core.Server.Services.BattleRetreat.Handlers;

/// <summary>
/// Applies "Try to get away." retreats authoritatively. A peer may only retreat its OWN party.
/// </summary>
internal class ServerBattleRetreatHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerBattleRetreatHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IBattleRetreatInterface retreatInterface;

    public ServerBattleRetreatHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IBattleRetreatInterface retreatInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.retreatInterface = retreatInterface;
        messageBroker.Subscribe<NetworkRequestBattleRetreat>(Handle);
        messageBroker.Subscribe<NetworkRequestBreakInCasualties>(HandleBreakInCasualties);
    }

    private void Handle(MessagePayload<NetworkRequestBattleRetreat> payload)
    {
        var obj = payload.What;
        var peer = (NetPeer)payload.Who;

        GameThread.RunSafe(() =>
        {
            // Ownership first: a peer may only ever retreat the party it controls.
            if (!playerManager.TryGetPlayer(peer, out var player) ||
                !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var owned) ||
                !objectManager.TryGetObjectWithLogging<MobileParty>(obj.PartyId, out var requested) ||
                !ReferenceEquals(owned, requested))
            {
                Logger.Warning("Peer requested a retreat for party {PartyId} it does not own", obj.PartyId);
                Reject(obj.PartyId);
                return;
            }

            if (!objectManager.TryGetObjectWithLogging<MapEvent>(obj.MapEventId, out var battle))
            {
                Reject(obj.PartyId);
                return;
            }

            // Two peers racing on the same battle are serialised by the game-thread queue; the second fails
            // the defender-side-leader re-validation inside TryApplyRetreat and is rejected here.
            if (!retreatInterface.TryApplyRetreat(requested, battle, out var campCleared))
            {
                Logger.Information("Retreat refused for party {PartyId}", obj.PartyId);
                Reject(obj.PartyId);
                return;
            }

            network.SendAll(new NetworkBattleRetreatResolved(obj.PartyId, approved: true, campCleared));
        });
    }

    private void HandleBreakInCasualties(MessagePayload<NetworkRequestBreakInCasualties> payload)
    {
        var obj = payload.What;
        var peer = (NetPeer)payload.Who;

        GameThread.RunSafe(() =>
        {
            // Same ownership rule as the retreat: a peer may only spend its own troops.
            if (!playerManager.TryGetPlayer(peer, out var player) ||
                !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var owned) ||
                !objectManager.TryGetObjectWithLogging<MobileParty>(obj.PartyId, out var requested) ||
                !ReferenceEquals(owned, requested))
            {
                Logger.Warning("Peer requested break-in losses for party {PartyId} it does not own", obj.PartyId);
                return;
            }

            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.SettlementId, out var settlement)) return;

            retreatInterface.ApplyBreakInCasualties(requested, settlement);
        });
    }

    private void Reject(string partyId)
        => network.SendAll(new NetworkBattleRetreatResolved(partyId, approved: false, System.Array.Empty<string>()));

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestBattleRetreat>(Handle);
        messageBroker.Unsubscribe<NetworkRequestBreakInCasualties>(HandleBreakInCasualties);
    }
}
