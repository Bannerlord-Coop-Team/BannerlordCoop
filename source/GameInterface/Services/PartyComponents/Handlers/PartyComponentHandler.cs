using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyComponents.Data;
using GameInterface.Services.PartyComponents.Messages;
using GameInterface.Services.PartyComponents.Patches.Lifetime;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.PartyComponents.Handlers;
internal class PartyComponentHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyComponentHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    private readonly Type[] partyTypes = new Type[]
    {
        typeof(BanditPartyComponent),
        typeof(CaravanPartyComponent),
        typeof(CustomPartyComponent),
        typeof(GarrisonPartyComponent),
        typeof(LordPartyComponent),
        typeof(MilitiaPartyComponent),
        typeof(VillagerPartyComponent),
    };

    public PartyComponentHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<PartyComponentCreated>(Handle);
        messageBroker.Subscribe<NetworkCreatePartyComponent>(Handle);

        messageBroker.Subscribe<PartyComponentMobilePartyChanged>(Handle);
        messageBroker.Subscribe<NetworkChangePartyComponentMobileParty>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyComponentCreated>(Handle);
        messageBroker.Unsubscribe<NetworkCreatePartyComponent>(Handle);
    }

    private void Handle(MessagePayload<NetworkChangePartyComponentMobileParty> payload)
    {
        // Check if Settlement sync is needed here as well for MilitiaPartyComponent
        var componentId = payload.What.ComponentId;
        var partyId = payload.What.PartyId;

        if(objectManager.TryGetObject<PartyComponent>(componentId, out var component) == false)
        {
            Logger.Error("Could not find PartyComponent with id {componentId}", componentId);
            return;
        }

        if (objectManager.TryGetObject<MobileParty>(partyId, out var party) == false)
        {
            Logger.Error("Could not find MobileParty with id {componentId}", partyId);
            return;
        }

        PartyComponentPatches.OverrideSetParty(component, party);
    }

    

    private void Handle(MessagePayload<PartyComponentMobilePartyChanged> payload)
    {
        var component = payload.What.Component;
        var party = payload.What.Party;
        
        if(objectManager.TryGetId(component, out var componentId) == false)
        {
            Logger.Error("PartyComponent was not registered with party PartyComponentRegistry");
            return;
        }

        var message = new NetworkChangePartyComponentMobileParty(componentId, party.StringId);
        network.SendAll(message);
    }

    private void Handle(MessagePayload<PartyComponentCreated> payload)

    {
        objectManager.AddNewObject(payload.What.Instance, out var id);
        // TODO: Check if party type is MilitiaPartyComponent and if thats the case use different patching to make sure (Home) Settlement is  sync as well
        // Note: Only found that PartyComponent is registered in obj manager, but not sure it is really created on clients gameInterface
        var typeIndex = partyTypes.IndexOf(payload.What.Instance.GetType());

        if (typeIndex == 5)
        {
            objectManager.TryGetId(payload.What.Instance.HomeSettlement, out var SettlementId);
            var MilitiaCompData = new PartyComponentData(typeIndex, id, SettlementId);
            
            network.SendAll(new NetworkCreatePartyComponent(MilitiaCompData));
        }
        else
        {

            var data = new PartyComponentData(typeIndex, id);
            network.SendAll(new NetworkCreatePartyComponent(data));
        }
    }

    private void Handle(MessagePayload<NetworkCreatePartyComponent> payload)
    {
        var data = payload.What.Data;
        var typeIdx = data.TypeIndex;
        // obj is off specific Party Component type like fe MilitiaPartyComponent

        if (typeIdx == 5)
        {
            var militiaObj = ObjectHelper.SkipConstructor(partyTypes[typeIdx]);

            if (!objectManager.TryGetObject<Settlement>(data.SettlementId, out var settlementObj)) 
            { 
                Logger.Error("Failed to retrieve Settlement");
                return; 
            }

            //militiaObj.Settlement = settlementObj;
            objectManager.AddExisting(data.Id, militiaObj);
        }
        else
        {
            PartyComponent obj = (PartyComponent)ObjectHelper.SkipConstructor(partyTypes[typeIdx]);
            objectManager.AddExisting(data.Id, obj);
        }
        
    }
}