using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MapEventSides.Handlers;
internal class MapEventSideLifetimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;


    public MapEventSideLifetimeHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<MapEventSideCreated>(Handle);
        messageBroker.Subscribe<NetworkCreateMapEventSide>(Handle);

        messageBroker.Subscribe<MapEventSideDestroyed>(Handle);
        messageBroker.Subscribe<NetworkDestroyMapEventSide>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MapEventSideCreated>(Handle);
        messageBroker.Unsubscribe<NetworkCreateMapEventSide>(Handle);

        messageBroker.Unsubscribe<MapEventSideDestroyed>(Handle);
        messageBroker.Unsubscribe<NetworkDestroyMapEventSide>(Handle);
    }

    private void Handle(MessagePayload<MapEventSideCreated> payload)
    {
        if (objectManager.AddNewObject(payload.What.Instance, out var mapEventSideId) == false) return;

        var missionSide = (int)payload.What.MissionSide;

        if (objectManager.TryGetId(payload.What.MapEvent, out var mapEventId) == false) return;
        if (objectManager.TryGetId(payload.What.LeaderParty.MobileParty, out var leaderMobilePartyId) == false) return;

        network.SendAll(new NetworkCreateMapEventSide(mapEventSideId, mapEventId, missionSide, leaderMobilePartyId));
    }

    private void Handle(MessagePayload<NetworkCreateMapEventSide> payload)
    {
        var data = payload.What;

        if (objectManager.TryGetObject<MapEvent>(data.MapEventId, out var mapEvent) == false) return;
        if (objectManager.TryGetObject<MobileParty>(data.MobilePartyId, out var mobileParty) == false) return;

        var battleSideEnum = (BattleSideEnum)data.BattleSide;

        var newMapEventSide = new MapEventSide(mapEvent, battleSideEnum, mobileParty.Party);

        objectManager.AddExisting(payload.What.MapEventSideId, newMapEventSide);
    }

    private void Handle(MessagePayload<MapEventSideDestroyed> payload)
    {
        var mapEventSide = payload.What.Instance;
        if (objectManager.TryGetId(mapEventSide, out var mapEventId) == false)
        {
            Logger.Error("Unable to get {type} if from {obj}", nameof(MapEventSide), mapEventSide);
            return;
        }

        objectManager.Remove(payload.What.Instance);

        network.SendAll(new NetworkDestroyMapEventSide(mapEventId));
    }

    private void Handle(MessagePayload<NetworkDestroyMapEventSide> payload)
    {
        if (objectManager.TryGetObject<MapEventSide>(payload.What.MapEventSideId, out var mapEvent) == false)
        {
            Logger.Error("Unable to get {type} if from {obj}", nameof(MapEvent), payload.What.MapEventSideId);
            return;
        }

        objectManager.Remove(mapEvent);
    }
}
