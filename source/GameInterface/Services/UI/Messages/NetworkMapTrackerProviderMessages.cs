using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkMapTrackerPartyCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    public NetworkMapTrackerPartyCreated(string mobilePartyId)
    {
        MobilePartyId = mobilePartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkMapTrackerPartyRemoved : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    public NetworkMapTrackerPartyRemoved(string mobilePartyId)
    {
        MobilePartyId = mobilePartyId;
    }
}
