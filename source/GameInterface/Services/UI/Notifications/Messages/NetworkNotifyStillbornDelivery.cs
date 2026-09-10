using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyStillbornDelivery : ICommand
{
    [ProtoMember(1)]
    public readonly string MotherCharacterId;

    public NetworkNotifyStillbornDelivery(string motherCharacterId)
    {
        MotherCharacterId = motherCharacterId;
    }
}
