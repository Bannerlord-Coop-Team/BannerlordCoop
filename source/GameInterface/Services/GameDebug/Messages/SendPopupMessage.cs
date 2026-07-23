using Common.Messaging;

namespace GameInterface.Services.GameDebug.Messages;

public record SendPopupMessage : ICommand
{
    public string Text { get; }

    public SendPopupMessage(string text)
    {
        Text = text;
    }
}
