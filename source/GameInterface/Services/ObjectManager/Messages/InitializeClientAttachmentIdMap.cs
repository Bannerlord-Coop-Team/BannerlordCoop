using Common.Messaging;

namespace GameInterface.Services.ObjectManager.Messages;

/// <summary>
/// Carries the server's <see cref="AttachmentIdMap"/> to a joining client as part of the save transfer,
/// so the client can re-key its live-created party attachments to the server's ids once all game objects
/// are registered (see AttachmentIdMapInitializationHandler).
/// </summary>
public record InitializeClientAttachmentIdMap : IEvent
{
    public AttachmentIdMap AttachmentIdMap;

    public InitializeClientAttachmentIdMap(AttachmentIdMap attachmentIdMap)
    {
        AttachmentIdMap = attachmentIdMap;
    }
}
