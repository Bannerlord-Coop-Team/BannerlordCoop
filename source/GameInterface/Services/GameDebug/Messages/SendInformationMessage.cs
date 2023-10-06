using Common.Messaging;

namespace GameInterface.Services.GameDebug.Messages;

public record SendInformationMessage : ICommand
{
    public string Text { get; }

    public SendInformationMessage(string text)
    {
        Text = text;
    }
}
