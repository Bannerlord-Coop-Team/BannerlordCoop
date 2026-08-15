using Common.Messaging;

namespace GameInterface.Services.UI.Messages;

public readonly struct ChatVisibilitySelected : IEvent
{
    public readonly bool ShowChat;

    public ChatVisibilitySelected(bool showChat)
    {
        ShowChat = showChat;
    }
}
