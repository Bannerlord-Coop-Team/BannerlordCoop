using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MBBodyProperties.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.Core;
namespace GameInterface.Services.MBBodyProperties.Handlers;


/// <summary>
/// Lifetime handler for <see cref="MBBodyProperty"/> objects.
/// </summary>
internal class MBBodyPropertyLifetimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MBBodyPropertyLifetimeHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    public MBBodyPropertyLifetimeHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<MBBodyPropertyCreated>(HandleCreatedEvent);
        messageBroker.Subscribe<NetworkCreateMBBodyProperty>(HandleCreateCommand);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MBBodyPropertyCreated>(HandleCreatedEvent);
        messageBroker.Unsubscribe<NetworkCreateMBBodyProperty>(HandleCreateCommand);
    }

    private void HandleCreatedEvent(MessagePayload<MBBodyPropertyCreated> payload)
    {
        if (!objectManager.AddNewObject(payload.What.Instance, out var id))
        {
            Logger.Error("Failed to AddNewObject on {EventHandler}", nameof(MBBodyPropertyCreated));
            return;
        }

        network.SendAll(new NetworkCreateMBBodyProperty(id));
    }

    // WARNING: This is a default generated implementation that might not work on all services, be sure to test and implement need logic
    private void HandleCreateCommand(MessagePayload<NetworkCreateMBBodyProperty> payload)
    {
        var newMBBodyProperty = ObjectHelper.SkipConstructor<MBBodyProperty>();
        if (!objectManager.AddExisting(payload.What.Id, newMBBodyProperty))
        {
            Logger.Error("Failed to create {ObjectName} on {EventHandler}", nameof(MBBodyProperty), nameof(NetworkCreateMBBodyProperty));
        }
    }
}
