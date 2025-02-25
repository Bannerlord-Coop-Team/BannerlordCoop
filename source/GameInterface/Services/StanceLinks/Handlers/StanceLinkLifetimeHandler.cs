using System;
using System.Runtime.Serialization;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Armies.Data;
using GameInterface.Services.Armies.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Stances.Data;
using GameInterface.Services.Stances.Messages.Lifetime;
using GameInterface.Services.Template.Messages;
using GameInterface.Services.Template.Patches;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.Core.ViewModelCollection.CharacterViewModel;

namespace GameInterface.Services.Template.Handlers;

/// <summary>
/// Handler for <see cref="StanceLink"/> messages
/// </summary>
public class StanceLinkLifetimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<StanceLinkLifetimeHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    public StanceLinkLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        //subscribe to internal and network messages
        messageBroker.Subscribe<StanceLinkCreated>(Handle_StanceLinkCreated);
        messageBroker.Subscribe<NetworkCreateStanceLink>(Handle_CreateStanceLink);
    }

    public void Dispose()
    {
        //unsubscribe to internal and network messages
        messageBroker.Unsubscribe<StanceLinkCreated>(Handle_StanceLinkCreated);
        messageBroker.Unsubscribe<NetworkCreateStanceLink>(Handle_CreateStanceLink);
    }

    private void Handle_StanceLinkCreated(MessagePayload<StanceLinkCreated> payload)
    {
        //create temp objects to manipulate data easier
        var stanceLink = payload.What.StanceLink;
        var stanceType = (short)(payload.What.StanceType);
        var faction1 = payload.What.Faction1;
        var faction2 = payload.What.Faction2;
        var isAtConstantWar = payload.What.IsAtConstantWar;

        //get ID of necessary object - if error abort
        if (objectManager.TryGetId(faction1, out var faction1Id) == false) return;
        if (objectManager.TryGetId(faction2, out var faction2Id) == false) return;

        //register object
        if (objectManager.AddNewObject(stanceLink, out var stanceLinkId) == false) return;

        //send network message to register object on client side
        var data = new StanceLinkCreationData(stanceLinkId, stanceType, faction1Id, faction2Id, isAtConstantWar);
        var message = new NetworkCreateStanceLink(data);
        network.SendAll(message);
    }


    private void Handle_CreateStanceLink(MessagePayload<NetworkCreateStanceLink> obj)
    {
        var payload = obj.What.Data;

        IFaction faction1;
        IFaction faction2;

        if (objectManager.TryGetObject(payload.Faction1Id, out Kingdom kingdom1) == false)
        {
            if(objectManager.TryGetObject(payload.Faction1Id, out Clan clan1) == false)
            {
                Logger.Error("Failed to get faction, {id}", payload.Faction1Id);
                return;
            }
            else
            {
                faction1 = clan1;
            }
        }
        else
        {
            faction1 = kingdom1;
        }

        if (objectManager.TryGetObject(payload.Faction2Id, out Kingdom kingdom2) == false)
        {
            if (objectManager.TryGetObject(payload.Faction2Id, out Clan clan2) == false)
            {
                Logger.Error("Failed to get faction, {id}", payload.Faction2Id);
                return;
            }
            else
            {
                faction2 = kingdom2;
            }
        }
        else
        {
            faction2 = kingdom2;
        }

        using(new AllowedThread())
        {
            var stanceLink = new StanceLink((StanceType)payload.StanceType, faction1, faction2, payload.IsAtConstantWar);
            if (objectManager.AddExisting(payload.StringId, stanceLink) == false)
            {
                Logger.Error("Failed to add existing StanceLink, {id}", payload.StringId);
                return;
            }
        }
    }
}