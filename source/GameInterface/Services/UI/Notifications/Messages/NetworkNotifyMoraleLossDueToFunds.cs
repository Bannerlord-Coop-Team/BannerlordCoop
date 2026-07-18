using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyMoraleLossDueToFunds : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    public NetworkNotifyMoraleLossDueToFunds(string mobilePartyId)
    {
        MobilePartyId = mobilePartyId;
    }
}
