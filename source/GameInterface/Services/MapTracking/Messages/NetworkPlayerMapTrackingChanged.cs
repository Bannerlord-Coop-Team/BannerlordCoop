using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapTracking.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerMapTrackingChanged : ICommand
{
    [ProtoMember(1)]
    public readonly string ControllerId;

    [ProtoMember(2)]
    public readonly string OtherControllerId;

    [ProtoMember(3)]
    public readonly bool IsTracking;

    public NetworkPlayerMapTrackingChanged(string controllerId, string otherControllerId, bool isTracking)
    {
        ControllerId = controllerId;
        OtherControllerId = otherControllerId;
        IsTracking = isTracking;
    }
}
