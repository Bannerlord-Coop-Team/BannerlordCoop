using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Template.Messages;
using Serilog;

namespace GameInterface.Services.Template.Handlers;

/// <summary>
/// This is a template handler for processing specific messages in the system.
/// It serves as an example for new developers on how to use the message broker, 
/// interact with the object manager, and handle network communication.
/// Handlers are automatically instantiated by <see cref="ServiceModule.GetHandlers"/>.
/// </summary>
public class TemplateHandler : IHandler
{
    private static ILogger Logger { get; } = LogManager.GetLogger<TemplateHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    /// <summary>
    /// Initializes a new instance of <see cref="TemplateHandler"/>.
    /// Dependencies are automatically injected by Autofac when the class is instantiated.
    /// </summary>
    /// <param name="messageBroker">Handles publishing and subscribing to messages.</param>
    /// <param name="objectManager">Manages game objects and their network IDs.</param>
    /// <param name="network">Handles network communication.</param>
    public TemplateHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        // Subscribe to specific message types so this handler can process them when they are published.
        messageBroker.Subscribe<TemplateEventMessage>(Handle_TemplateEventMessage);
        messageBroker.Subscribe<TemplateNetworkMessage>(Handle_TemplateNetworkMessage);
    }

    /// <summary>
    /// Cleans up message subscriptions when this handler is disposed.
    /// This prevents memory leaks by ensuring the handler instance is not kept alive unnecessarily.
    /// </summary>
    public void Dispose()
    {
        messageBroker.Unsubscribe<TemplateNetworkMessage>(Handle_TemplateNetworkMessage);
        messageBroker.Unsubscribe<TemplateNetworkMessage>(Handle_TemplateNetworkMessage);
    }

    /// <summary>
    /// Handles event messages published from a Harmony patch.
    /// This method listens for TemplateEventMessages and processes them accordingly.
    /// </summary>
    /// <param name="payload">The message payload containing event data.</param>
    private void Handle_TemplateEventMessage(MessagePayload<TemplateEventMessage> payload)
    {
        var instance = payload.What.Instance;
        var value = payload.What.Value;

        // Attempt to retrieve the network ID for the given instance.
        // If the instance has no network ID, log an error and return.
        if (!objectManager.TryGetId(instance, out var networkId))
        {
            Logger.Error("Unable to get network ID for instance of type {type}", instance.GetType());
            return;
        }

        var message = new TemplateNetworkMessage(networkId, value);

        // This message is sent to all connected clients.
        // This logic only runs on the server.
        network.SendAll(message);
    }

    /// <summary>
    /// Handles network messages that modify application state.
    /// These messages typically originate from a client and affect game state on the server.
    /// </summary>
    /// <param name="payload">The message payload containing network data.</param>
    private void Handle_TemplateNetworkMessage(MessagePayload<TemplateNetworkMessage> payload)
    {
        // Ensure the logic is executed on the main thread.
        GameLoopRunner.RunOnMainThread(() =>
        {
            /// This ensures that the original function is executed correctly within an allowed thread context.
            using (new AllowedThread())
            {
                // Implement instance-specific logic here.
            }
        }, blocking: true);
    }
}
