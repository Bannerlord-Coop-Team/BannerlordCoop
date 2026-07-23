using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyComponents.Messages;

[ProtoContract]
internal readonly struct NetworkPartyComponentMobilePartyUpdated : ICommand
{
    [ProtoMember(1)]
    public readonly string InstanceId;
    [ProtoMember(2)]
    public readonly string MobilePartyId;

    public NetworkPartyComponentMobilePartyUpdated(string instanceId, string mobilePartyId)
    {
        InstanceId = instanceId;
        MobilePartyId = mobilePartyId;
    }
}
