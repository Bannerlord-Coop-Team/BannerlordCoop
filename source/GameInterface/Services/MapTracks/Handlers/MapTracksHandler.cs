using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Caravans.Handlers;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MapTracks.Interfaces;
using GameInterface.Services.MapTracks.Messages;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapTracks.Handlers;

internal class MapTracksHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CaravansCampaignBehaviorHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IMapTracksCampaignBehaviorInterface mapTracksCampaignBehaviorInterface;

    public MapTracksHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IMapTracksCampaignBehaviorInterface mapTracksCampaignBehaviorInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.mapTracksCampaignBehaviorInterface = mapTracksCampaignBehaviorInterface;

        messageBroker.Subscribe<UpdateClientsMapTrackData>(Handle_UpdateClientsMapTrackData);
        messageBroker.Subscribe<NetworkUpdateClientsMapTrackData>(Handle_NetworkUpdateClientsMapTrackData);

        messageBroker.Subscribe<PlayerHeroChanged>(Handle_PlayerHeroChanged);
        messageBroker.Subscribe<NetworkInitializePlayerTracksKeys>(Handle_NetworkInitializePlayerTracksKeys);

        messageBroker.Subscribe<NetworkUpdateClientInitialVisibleTracks>(Handle_NetworkUpdateClientInitialVisibleTracks);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<UpdateClientsMapTrackData>(Handle_UpdateClientsMapTrackData);
        messageBroker.Unsubscribe<NetworkUpdateClientsMapTrackData>(Handle_NetworkUpdateClientsMapTrackData);

        messageBroker.Unsubscribe<PlayerHeroChanged>(Handle_PlayerHeroChanged);
        messageBroker.Unsubscribe<NetworkInitializePlayerTracksKeys>(Handle_NetworkInitializePlayerTracksKeys);

        messageBroker.Unsubscribe<NetworkUpdateClientInitialVisibleTracks>(Handle_NetworkUpdateClientInitialVisibleTracks);
    }

    private void Handle_UpdateClientsMapTrackData(MessagePayload<UpdateClientsMapTrackData> obj)
    {
        GameThread.RunSafe(() =>
        {
            var message = new NetworkUpdateClientsMapTrackData(obj.What.VisibleTrackChange, obj.What.IsRemovingTracks);
            network.SendAll(message);
        });
    }

    private void Handle_NetworkUpdateClientsMapTrackData(MessagePayload<NetworkUpdateClientsMapTrackData> obj)
    {
        // Update tracks on clients
        GameThread.RunSafe(() =>
        {
            if (!TryGetMapTracksBehavior(out var mapTracksBehavior)) return;

            foreach (var playerTrackChanges in obj.What.VisibleTrackChange)
            {
                if (!objectManager.TryGetObjectWithLogging<MobileParty>(playerTrackChanges.Key, out var playerParty)) continue;

                // Only update tracks for associated player
                if (playerParty != MobileParty.MainParty) continue;

                var visibleTrackChanges = playerTrackChanges.Value;
                mapTracksCampaignBehaviorInterface.ApplyVisibleTrackChanges(mapTracksBehavior, visibleTrackChanges, obj.What.IsRemovingTracks);
            }
        });
    }

    private void Handle_PlayerHeroChanged(MessagePayload<PlayerHeroChanged> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.NewHero.PartyBelongedTo, out var playerPartyId)) return;

            network.SendAll(new NetworkInitializePlayerTracksKeys(playerPartyId));
        });
    }

    private void Handle_NetworkInitializePlayerTracksKeys(MessagePayload<NetworkInitializePlayerTracksKeys> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetMapTracksBehavior(out var mapTracksBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.PlayerPartyId, out var playerParty)) return;

            mapTracksCampaignBehaviorInterface.AddPlayerPartyKeys(obj.What.PlayerPartyId);

            // Track data is not kept as part of the save game even in vanilla Bannerlord.
            // When a player joins, calculate the tracks for their party and update
            var visibleTrackChanges = mapTracksCampaignBehaviorInterface.DetectTracksForPlayerParty(mapTracksBehavior, playerParty);

            if (visibleTrackChanges.Count > 0)
            {
                network.Send(obj.Who as NetPeer, new NetworkUpdateClientInitialVisibleTracks(visibleTrackChanges));
            }
        });
    }

    private void Handle_NetworkUpdateClientInitialVisibleTracks(MessagePayload<NetworkUpdateClientInitialVisibleTracks> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetMapTracksBehavior(out var mapTracksBehavior)) return;

            mapTracksCampaignBehaviorInterface.ApplyVisibleTrackChanges(mapTracksBehavior, obj.What.VisibleTrackChanges, false);
        });
    }

    private bool TryGetMapTracksBehavior(out MapTracksCampaignBehavior mapTracksBehavior)
    {
        mapTracksBehavior = Campaign.Current?.GetCampaignBehavior<MapTracksCampaignBehavior>();
        if (mapTracksBehavior != null) return true;

        Logger.Debug("Skipping map tracks update because the campaign behavior is unavailable");
        return false;
    }
}
