using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyAnimalsSlaughteredToEat : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    public NetworkNotifyAnimalsSlaughteredToEat(string mobilePartyId)
    {
        MobilePartyId = mobilePartyId;
    }
}
