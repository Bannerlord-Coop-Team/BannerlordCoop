﻿using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.BesiegerCamps.Messages;
using GameInterface.Services.BesiegerCamps.Messages.Collection;
using GameInterface.Services.BesiegerCampss.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using static GameInterface.Services.BesiegerCamps.Extensions.BesiegerCampExtensions;

namespace GameInterface.Services.BesiegerCamps.Handlers;

/// <summary>
/// Handler for  <see cref="BesiegerCamp._besiegerParties"/>
/// </summary>
internal class BesiegerCampBesiegerPartyHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BesiegerCampBesiegerPartyHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public BesiegerCampBesiegerPartyHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<NetworkAddBesiegerParty>(HandleCommand_AddBesiegerParty);
        messageBroker.Subscribe<NetworkRemoveBesiegerParty>(HandleCommand_RemoveBesiegerParty);
        messageBroker.Subscribe<BesiegerPartyAdded>(HandleEvent_BesiegerPartyAdded);
        messageBroker.Subscribe<BesiegerPartyRemoved>(HandleEvent_BesiegerPartyRemoved);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkAddBesiegerParty>(HandleCommand_AddBesiegerParty);
        messageBroker.Unsubscribe<NetworkRemoveBesiegerParty>(HandleCommand_RemoveBesiegerParty);
        messageBroker.Unsubscribe<BesiegerPartyAdded>(HandleEvent_BesiegerPartyAdded);
        messageBroker.Unsubscribe<BesiegerPartyRemoved>(HandleEvent_BesiegerPartyRemoved);
    }

    private void HandleEvent_BesiegerPartyAdded(MessagePayload<BesiegerPartyAdded> payload)
    {
        var data = payload.What;

        var networkData = CreateNetworkMessageData(data.BesiegerCamp, data.BesiegerParty);
        if (networkData == null) return;

        network.SendAll(new NetworkAddBesiegerParty(networkData));
    }

    private void HandleEvent_BesiegerPartyRemoved(MessagePayload<BesiegerPartyRemoved> payload)
    {
        var data = payload.What;

        var networkData = CreateNetworkMessageData(data.BesiegerCamp, data.BesiegerParty);
        if (networkData == null) return;

        network.SendAll(new NetworkRemoveBesiegerParty(networkData));
    }

    private void HandleCommand_RemoveBesiegerParty(MessagePayload<NetworkRemoveBesiegerParty> payload)
    {
        var data = payload.What;
        var instanceId = data.BesiegerCampId;
        var removedPartyId = data.BesiegerPartyId;

        if (objectManager.TryGetObject<BesiegerCamp>(instanceId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(BesiegerCamp), instanceId);
            return;
        }

        if (objectManager.TryGetObject<MobileParty>(removedPartyId, out var removedParty) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), removedPartyId);
            return;
        }

        instance._besiegerParties.Remove(removedParty);
    }

    private void HandleCommand_AddBesiegerParty(MessagePayload<NetworkAddBesiegerParty> payload)
    {
        var data = payload.What;
        var instanceId = data.BesiegerCampId;
        var addedPartyId = data.BesiegerPartyId;

        if (objectManager.TryGetObject<BesiegerCamp>(instanceId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(BesiegerCamp), instanceId);
            return;
        }

        if (objectManager.TryGetObject<MobileParty>(addedPartyId, out var addedParty) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(MobileParty), addedPartyId);
            return;
        }

        instance._besiegerParties.Add(addedParty);
    }

    private BesiegerPartyData CreateNetworkMessageData(BesiegerCamp camp, MobileParty party)
    {
        var campId = objectManager.TryGetId(camp, Logger);
        var partyId = objectManager.TryGetId(party, Logger);

        return (campId != null && partyId != null) ? new BesiegerPartyData(campId, partyId) : null;
    }

}