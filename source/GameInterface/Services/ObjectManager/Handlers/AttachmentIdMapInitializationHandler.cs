using Common;
using Common.Messaging;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager.Messages;

namespace GameInterface.Services.ObjectManager.Handlers;

/// <summary>
/// On a joining client, hands the server's <see cref="AttachmentIdMap"/> to the registry factory so the next
/// RegisterAllObjects registers each live-created attachment directly under the server's id
/// (see <see cref="IAutoRegistryFactory.SetJoinIdRemap"/>), instead of re-deriving an owner-keyed id and
/// re-keying afterward. The map arrives on the save transfer, before the campaign loads and registers its objects.
/// </summary>
internal class AttachmentIdMapInitializationHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IAutoRegistryFactory autoRegistryFactory;

    public AttachmentIdMapInitializationHandler(IMessageBroker messageBroker, IAutoRegistryFactory autoRegistryFactory)
    {
        this.messageBroker = messageBroker;
        this.autoRegistryFactory = autoRegistryFactory;

        messageBroker.Subscribe<InitializeClientAttachmentIdMap>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<InitializeClientAttachmentIdMap>(Handle);
    }

    private void Handle(MessagePayload<InitializeClientAttachmentIdMap> payload)
    {
        if (ModInformation.IsServer) return;

        autoRegistryFactory.SetJoinIdRemap(payload.What.AttachmentIdMap?.DerivedToServerId);
    }
}
