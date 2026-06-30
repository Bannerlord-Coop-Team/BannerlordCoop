using Common.Messaging;
using GameInterface.Registry.Messages;
using GameInterface.Services.ObjectManager.Messages;

namespace GameInterface.Services.ObjectManager.Handlers;

/// <summary>
/// On a joining client, applies the server's <see cref="AttachmentIdMap"/> right after all game objects
/// are registered. The map is stashed when the save transfer arrives, then applied at
/// <see cref="AllGameObjectsRegistered"/> — after RegisterAllObjects has registered each attachment under
/// its re-derived id and before world deltas flush — re-keying live-created attachments to the server's ids.
/// </summary>
internal class AttachmentIdMapInitializationHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IAttachmentIdMapper mapper;

    private AttachmentIdMap pendingMap;

    public AttachmentIdMapInitializationHandler(IMessageBroker messageBroker, IAttachmentIdMapper mapper)
    {
        this.messageBroker = messageBroker;
        this.mapper = mapper;

        messageBroker.Subscribe<InitializeClientAttachmentIdMap>(Handle);
        messageBroker.Subscribe<AllGameObjectsRegistered>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<InitializeClientAttachmentIdMap>(Handle);
        messageBroker.Unsubscribe<AllGameObjectsRegistered>(Handle);
    }

    private void Handle(MessagePayload<InitializeClientAttachmentIdMap> payload)
    {
        pendingMap = payload.What.AttachmentIdMap;
    }

    private void Handle(MessagePayload<AllGameObjectsRegistered> payload)
    {
        if (pendingMap == null) return;

        mapper.ApplyClientMap(pendingMap);
        pendingMap = null;
    }
}
