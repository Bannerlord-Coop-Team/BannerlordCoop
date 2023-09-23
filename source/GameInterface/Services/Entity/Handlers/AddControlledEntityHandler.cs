using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Entity.Messages;
using Serilog;

namespace GameInterface.Services.Entity.Handlers;

internal class AddControlledEntityHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<AddControlledEntityHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IControlledEntityRegistry controlledEntityRegistry;

    public AddControlledEntityHandler(
        IMessageBroker messageBroker,
        IControlledEntityRegistry controlledEntityRegistry)
    {
        this.messageBroker = messageBroker;
        this.controlledEntityRegistry = controlledEntityRegistry;
        messageBroker.Subscribe<AddControlledEntity>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AddControlledEntity>(Handle);
    }

    private void Handle(MessagePayload<AddControlledEntity> obj)
    {
        var controllerId = obj.What.ControllerId;
        var entityId = obj.What.EntityId;

        if (controlledEntityRegistry.RegisterAsControlled(controllerId, entityId) == false)
        {
            Logger.Error("Unable to register {entityId} entity with {controllerId} controller", controllerId, entityId);
        }
    }
}
