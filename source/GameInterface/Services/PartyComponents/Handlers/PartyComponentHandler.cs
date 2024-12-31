using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyComponents.Data;
using GameInterface.Services.PartyComponents.Messages;
using GameInterface.Services.PartyComponents.Patches;
using Serilog;
using System;
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

        messageBroker.Unsubscribe<PartyComponentMobilePartyChanged>(Handle);
        messageBroker.Unsubscribe<NetworkChangePartyComponentMobileParty>(Handle);
    }

    private void Handle(MessagePayload<NetworkChangePartyComponentMobileParty> payload)
    {
        var componentId = payload.What.ComponentId;
        var partyId = payload.What.PartyId;

        if (objectManager.TryGetObject<PartyComponent>(componentId, out var component) == false)
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

        if (objectManager.TryGetId(component, out var componentId) == false)
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

        var typeIndex = partyTypes.IndexOf(payload.What.Instance.GetType());
        var data = new PartyComponentData(typeIndex, id);
        
        network.SendAll(new NetworkCreatePartyComponent(data));
        
        // This is needed to enforce calling MilitiaPartyComponent settlement patch since otherwise the patch is never called
        if (payload.What.SettlementId != null)
        {
            MilitiaPartyComponent militiaParty = payload.What.Instance as MilitiaPartyComponent;
            if (objectManager.TryGetObject<Settlement>(payload.What.SettlementId, out var settlement)) militiaParty.Settlement = settlement;
            else Logger.Error("Could not find Settlement with id {settlementId} \n"
                + "Callstack: {callstack}", payload.What.SettlementId, Environment.StackTrace);
        }
    }

    private void Handle(MessagePayload<NetworkCreatePartyComponent> payload)
    {
        var data = payload.What.Data;
        var typeIdx = data.TypeIndex;

        var obj = ObjectHelper.SkipConstructor(partyTypes[typeIdx]);

        objectManager.AddExisting(data.Id, obj);
    }
}