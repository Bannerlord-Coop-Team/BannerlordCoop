using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobileParties.Messages.Lifetime;

[ProtoContract]
internal readonly struct NetworkApplyDestroyParty : ICommand
{
    [ProtoMember(1)]
    public readonly string VictoriousPartyId;
    [ProtoMember(2)]
    public readonly string DefeatedPartyId;

    public NetworkApplyDestroyParty(string victorousPartyId, string defeatedPartyId)
    {
        VictoriousPartyId = victorousPartyId;
        DefeatedPartyId = defeatedPartyId;
    }
}
