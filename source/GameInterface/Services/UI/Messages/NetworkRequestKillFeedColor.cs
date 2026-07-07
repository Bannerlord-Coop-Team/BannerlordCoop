using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRequestKillFeedColor : ICommand
{
    [ProtoMember(1)]
    public readonly int Red;

    [ProtoMember(2)]
    public readonly int Green;

    [ProtoMember(3)]
    public readonly int Blue;

    public NetworkRequestKillFeedColor(int red, int green, int blue)
    {
        Red = red;
        Green = green;
        Blue = blue;
    }
}
