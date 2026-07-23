using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.GameDebug.Messages;

[ProtoContract(SkipConstructor = true)]
public record SendInformationMessage : ICommand
{
    [ProtoMember(1)]
    public string Text { get; }

    public SendInformationMessage(string text)
    {
        Text = text;
    }
}
