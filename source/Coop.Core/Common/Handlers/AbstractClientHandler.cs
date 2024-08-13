using Common.Messaging;
using Coop.Core.Common.Handlers.NetworkCommands;
using Coop.Core.Common.Handlers.NetworkCommands.MBTypes;
using GameInterface.Common.Commands;
using GameInterface.Common.Commands.MBTypes;

namespace Coop.Core.Common.Handlers;

public abstract class AbstractClientHandler<TClass> : IHandler
{
    protected readonly IMessageBroker messageBroker;
    
    public AbstractClientHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<NetworkBoolCommand>(Handle, typeof(TClass).Name);
        messageBroker.Subscribe<NetworkFloatCommand>(Handle, typeof(TClass).Name);
        messageBroker.Subscribe<NetworkIntCommand>(Handle, typeof(TClass).Name);
        messageBroker.Subscribe<NetworkLongCommand>(Handle, typeof(TClass).Name);
        messageBroker.Subscribe<NetworkStringCommand>(Handle, typeof(TClass).Name);
        messageBroker.Subscribe<NetworkTextObjectCommand>(HandleTextObject, typeof(TClass).Name);
        messageBroker.Subscribe<NetworkStringCommand>(HandleEquipmentObject, $"{typeof(TClass).Name}_Equipment");
        messageBroker.Subscribe<NetworkLongCommand>(HandleCampaignTimeObject, $"{typeof(TClass).Name}_CampaignTime");
        messageBroker.Subscribe<NetworkStringCommand>(HandleTrackedObject, $"{typeof(TClass).Name}_TrackedObject");
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBoolCommand>(Handle, typeof(TClass).Name);
        messageBroker.Unsubscribe<NetworkFloatCommand>(Handle, typeof(TClass).Name);
        messageBroker.Unsubscribe<NetworkIntCommand>(Handle, typeof(TClass).Name);
        messageBroker.Unsubscribe<NetworkLongCommand>(Handle, typeof(TClass).Name);
        messageBroker.Unsubscribe<NetworkStringCommand>(Handle, typeof(TClass).Name);
        messageBroker.Unsubscribe<NetworkTextObjectCommand>(HandleTextObject, typeof(TClass).Name);
        messageBroker.Unsubscribe<NetworkStringCommand>(HandleEquipmentObject, $"{typeof(TClass).Name}_Equipment");
        messageBroker.Unsubscribe<NetworkLongCommand>(HandleCampaignTimeObject, $"{typeof(TClass).Name}_CampaignTime");
        messageBroker.Unsubscribe<NetworkStringCommand>(HandleTrackedObject, $"{typeof(TClass).Name}_TrackedObject");
        Unsubscribe();
    }
    
    private void Handle(MessagePayload<NetworkBoolCommand> payload)
    {
        var data = payload.What;
        messageBroker.Publish(this, new GenericChangeCommand<TClass>(data.Id, data.Value, data.Target));
    }
    
    private void Handle(MessagePayload<NetworkFloatCommand> payload)
    {
        var data = payload.What;
        messageBroker.Publish(this, new GenericChangeCommand<TClass>(data.Id, data.Value, data.Target));
    }
    
    private void Handle(MessagePayload<NetworkIntCommand> payload)
    {
        var data = payload.What;
        messageBroker.Publish(this, new GenericChangeCommand<TClass>(data.Id, data.Value, data.Target));
    }
    
    private void Handle(MessagePayload<NetworkLongCommand> payload)
    {
        var data = payload.What;
        messageBroker.Publish(this, new GenericChangeCommand<TClass>(data.Id, data.Value, data.Target));
    }
    
    private void Handle(MessagePayload<NetworkStringCommand> payload)
    {
        var data = payload.What;
        messageBroker.Publish(this, new GenericChangeCommand<TClass>(data.Id, data.Value, data.Target));
    }
    
    private void HandleTextObject(MessagePayload<NetworkTextObjectCommand> payload)
    {
        var data = payload.What;
        messageBroker.Publish(this, new TextObjectChangeCommand<TClass>(data.Id, data.Value, data.Target));
    }
    
    private void HandleEquipmentObject(MessagePayload<NetworkStringCommand> payload)
    {
        var data = payload.What;
        messageBroker.Publish(this, new GenericChangeCommand<TClass, string>(data.Id, data.Value, data.Target), $"{typeof(TClass).Name}_Equipment");
    }
    
    private void HandleCampaignTimeObject(MessagePayload<NetworkLongCommand> payload)
    {
        var data = payload.What;
        messageBroker.Publish(this, new GenericChangeCommand<TClass, long>(data.Id, data.Value, data.Target), $"{typeof(TClass).Name}_CampaignTime");
    }
    
    private void HandleTrackedObject(MessagePayload<NetworkStringCommand> payload)
    {
        var data = payload.What;
        messageBroker.Publish(this, new GenericChangeCommand<TClass, string>(data.Id, data.Value, data.Target), $"{typeof(TClass).Name}_TrackedObject");
    }

    protected abstract void Unsubscribe();
}