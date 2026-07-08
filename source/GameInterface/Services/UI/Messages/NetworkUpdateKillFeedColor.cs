using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkUpdateKillFeedColor : IEvent
{
    [ProtoMember(1)]
    public readonly string ControllerId;

    [ProtoMember(2)]
    public readonly int Red;

    [ProtoMember(3)]
    public readonly int Green;

    [ProtoMember(4)]
    public readonly int Blue;

    public NetworkUpdateKillFeedColor(string controllerId, int red, int green, int blue)
    {
        ControllerId = controllerId;
        Red = red;
        Green = green;
        Blue = blue;
    }
}
