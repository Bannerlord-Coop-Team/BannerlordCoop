using System;
using System.Runtime.Serialization;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Armies.Data;
using GameInterface.Services.Armies.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Stances.Data;
using GameInterface.Services.Stances.Messages.Lifetime;
using GameInterface.Services.Template.Messages;
using GameInterface.Services.Template.Patches;
using Serilog;
using TaleWorlds.CampaignSystem;
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
        objectManager.AddNewObject(stanceLink, out var stanceLinkId);

        //send network message to register object on client side
        var data = new StanceLinkCreationData(stanceLinkId, stanceType, faction1Id, faction2Id, isAtConstantWar);
        var message = new NetworkCreateStanceLink(data);
        network.SendAll(message);
    }


    private void Handle_CreateStanceLink(MessagePayload<NetworkCreateStanceLink> payload)
    {
        Logger.Error("BIIIIIIIITE", typeof(StanceLink));
    }
}