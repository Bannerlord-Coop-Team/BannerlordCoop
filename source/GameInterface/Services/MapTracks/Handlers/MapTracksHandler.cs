using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Caravans.Handlers;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MapTracks.Interfaces;
using GameInterface.Services.MapTracks.Messages;
using GameInterface.Services.ObjectManager;
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
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<UpdateClientsMapTrackData>(Handle_UpdateClientsMapTrackData);
        messageBroker.Unsubscribe<NetworkUpdateClientsMapTrackData>(Handle_NetworkUpdateClientsMapTrackData);

        messageBroker.Unsubscribe<PlayerHeroChanged>(Handle_PlayerHeroChanged);
        messageBroker.Unsubscribe<NetworkInitializePlayerTracksKeys>(Handle_NetworkInitializePlayerTracksKeys);
    }

    private void Handle_UpdateClientsMapTrackData(MessagePayload<UpdateClientsMapTrackData> obj)
    {
        GameThread.RunSafe(() =>
        {
            var message = new NetworkUpdateClientsMapTrackData(obj.What.PlayerMapTracksData);
            network.SendAll(message);
        });
    }

    private void Handle_NetworkUpdateClientsMapTrackData(MessagePayload<NetworkUpdateClientsMapTrackData> obj)
    {
        if (!TryGetMapTracksBehavior(out var mapTracksBehavior)) return;

        // Update tracks on clients
        GameThread.RunSafe(() =>
        {
            if (!TryGetMapTracksBehavior(out var mapTracksBehavior)) return;

            foreach (var playerTracks in obj.What.PlayerMapTracksData.PlayerDetectedTracks)
            {
                if (!objectManager.TryGetObjectWithLogging<MobileParty>(playerTracks.Key, out var playerParty)) continue;

                // Only update tracks for associated player
                if (playerParty != MobileParty.MainParty) continue;

                // Clear existing tracks
                foreach (var track in mapTracksBehavior._detectedTracksCache)
                {
                    CampaignEventDispatcher.Instance.TrackLost(track);
                }

                // Update with new tracks
                mapTracksBehavior._detectedTracksCache = playerTracks.Value;
                foreach (var track in mapTracksBehavior._detectedTracksCache)
                {
                    track.IsDetected = true;
                    //track.IsEnemy = FactionManager.IsAtWarAgainstFaction(Hero.MainHero.MapFaction, party.MapFaction);
                    CampaignEventDispatcher.Instance.TrackDetected(track);
                }
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
            
            // Track data is not kept as part of the save game even in vanilla Bannerlord.
            // When a player joins, calculate the tracks for their party and update
            var shouldUpdateClients = mapTracksCampaignBehaviorInterface.DetectTracksForPlayerParty(mapTracksBehavior, playerParty);
            if (shouldUpdateClients)
            {
                mapTracksCampaignBehaviorInterface.PublishUpdateClientsMapTrackData();
            }

            mapTracksCampaignBehaviorInterface.AddPlayerPartyKeys(obj.What.PlayerPartyId);
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
