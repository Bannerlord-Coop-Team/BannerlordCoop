using System;
using Common.Messaging;
using Common.Messaging.MBTypes;
using Common.Network;
using Coop.Core.Common.Handlers.NetworkCommands;
using Coop.Core.Common.Handlers.NetworkCommands.MBTypes;

namespace Coop.Core.Common.Handlers;

public abstract class AbstractServerHandler<TClass> : IHandler
{
    protected readonly IMessageBroker messageBroker;
    protected readonly INetwork network;
    
    public AbstractServerHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        messageBroker.Subscribe<GenericChangedEvent<TClass>>(Handle);
        messageBroker.Subscribe<TextObjectChangedEvent<TClass>>(HandleTextObject);
        messageBroker.Subscribe<GenericChangedEvent<TClass, string>>(HandleEquipmentObject, "Equipment");
        messageBroker.Subscribe<GenericChangedEvent<TClass, long>>(HandleCampaignTimeObject, "CampaignTime");
        messageBroker.Subscribe<GenericChangedEvent<TClass, string>>(HandleTrackedObject, "TrackedObject");
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<GenericChangedEvent<TClass>>(Handle);
        messageBroker.Unsubscribe<TextObjectChangedEvent<TClass>>(HandleTextObject);
        messageBroker.Unsubscribe<GenericChangedEvent<TClass, string>>(HandleEquipmentObject, "Equipment");
        messageBroker.Unsubscribe<GenericChangedEvent<TClass, long>>(HandleCampaignTimeObject, "CampaignTime");
        messageBroker.Unsubscribe<GenericChangedEvent<TClass, string>>(HandleTrackedObject, "TrackedObject");
        Unsubscribe();
    }
    
    private void Handle(MessagePayload<GenericChangedEvent<TClass>> payload)
    {
        var data = payload.What;

        switch (data.Value)
        {
            case int intValue:
                network.SendAll(new NetworkIntCommand(data.Id, intValue, data.Target), typeof(TClass).Name);
                break;
            case string stringValue:
                network.SendAll(new NetworkStringCommand(data.Id, stringValue, data.Target), typeof(TClass).Name);
                break;
            case float floatValue:
                network.SendAll(new NetworkFloatCommand(data.Id, floatValue, data.Target), typeof(TClass).Name);
                break;
            case bool boolValue:
                network.SendAll(new NetworkBoolCommand(data.Id, boolValue, data.Target), typeof(TClass).Name);
                break;
            case long longValue:
                network.SendAll(new NetworkLongCommand(data.Id, longValue, data.Target), typeof(TClass).Name);
                break;
        }
    }
    
    private void HandleTextObject(MessagePayload<TextObjectChangedEvent<TClass>> payload)
    {
        var data = payload.What;

        network.SendAll(new NetworkTextObjectCommand(data.Id, data.Value, data.Target), typeof(TClass).Name);
    }
    
    private void HandleEquipmentObject(MessagePayload<GenericChangedEvent<TClass, string>> payload)
    {
        var data = payload.What;

        network.SendAll(new NetworkStringCommand(data.Id, data.Value, data.Target), payload.SubKey);
    }
    
    private void HandleCampaignTimeObject(MessagePayload<GenericChangedEvent<TClass, long>> payload)
    {
        var data = payload.What;

        network.SendAll(new NetworkLongCommand(data.Id, data.Value, data.Target), payload.SubKey);
    }
    
    private void HandleTrackedObject(MessagePayload<GenericChangedEvent<TClass, string>> payload)
    {
        var data = payload.What;

        network.SendAll(new NetworkStringCommand(data.Id, data.Value, data.Target), payload.SubKey);
    }

    protected abstract void Unsubscribe();
}