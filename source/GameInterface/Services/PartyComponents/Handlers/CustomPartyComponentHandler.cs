using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyComponents.Messages;
using GameInterface.Services.PartyComponents.Patches.CustomPartyComponents;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace GameInterface.Services.PartyComponents.Handlers;

internal class CustomPartyComponentHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    private static readonly ILogger Logger = LogManager.GetLogger<CustomPartyComponentHandler>();

    public CustomPartyComponentHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<CustomPartyComponentUpdated>(Handle);
        messageBroker.Subscribe<NetworkUpdateCustomPartyComponent>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CustomPartyComponentUpdated>(Handle);
        messageBroker.Unsubscribe<NetworkUpdateCustomPartyComponent>(Handle);
    }

    private void Handle(MessagePayload<CustomPartyComponentUpdated> payload)
    {
        var obj = payload.What;

        if(objectManager.TryGetId(obj.Component, out string componentId) == false)
        {
            Logger.Error("Could not find {component} in registry \n"
                + "Callstack: {callstack}", obj.Component, Environment.StackTrace);
            return;
        }

        NetworkUpdateCustomPartyComponent message = new(componentId, (int)obj.ComponentType, obj.NewValue);

        network.SendAll(message);
    }

    private void Handle(MessagePayload<NetworkUpdateCustomPartyComponent> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObject<CustomPartyComponent>(obj.ComponentId, out var component) == false)
        {
            Logger.Error("Could not find {component} in registry \n"
                + "Callstack: {callstack}", obj.ComponentId, Environment.StackTrace);
            return;
        }

        using (new AllowedThread())
        {
            switch ((CustomPartyComponentType)obj.CustomPartyComponentType)
            {
                case CustomPartyComponentType.Name:
                    component._name = new TextObject(obj.Value);
                    break;

                case CustomPartyComponentType.HomeSettlement:
                    objectManager.TryGetObject(obj.Value, out Settlement settlement);
                    component._homeSettlement = settlement;
                    break;

                case CustomPartyComponentType.Owner:
                    objectManager.TryGetObject(obj.Value, out Hero hero);
                    component._owner = hero;
                    break;

                case CustomPartyComponentType.BaseSpeed:
                    component._customPartyBaseSpeed = float.Parse(obj.Value);
                    break;

                case CustomPartyComponentType.MountId:
                    component._partyMountStringId = obj.Value;
                    break;

                case CustomPartyComponentType.HarnessId:
                    component._partyHarnessStringId = obj.Value;
                    break;

                case CustomPartyComponentType.AvoidHostileActions:
                    component._avoidHostileActions = bool.Parse(obj.Value);
                    break;
            }
        }
    }
}
