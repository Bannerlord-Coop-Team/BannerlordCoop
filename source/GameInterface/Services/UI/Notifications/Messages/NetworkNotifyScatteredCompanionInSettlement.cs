using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyScatteredCompanionInSettlement : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    [ProtoMember(2)]
    public readonly string CompanionId;

    public NetworkNotifyScatteredCompanionInSettlement(
        string mobilePartyId,
        string companionId)
    {
        MobilePartyId = mobilePartyId;
        CompanionId = companionId;
    }
}
