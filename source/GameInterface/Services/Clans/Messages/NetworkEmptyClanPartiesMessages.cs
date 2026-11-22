using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkEmptyClanPartyAdded : ICommand
{
    [ProtoMember(1)]
    public readonly string LeaderId;

    [ProtoMember(2)]
    public readonly string[] ShipIds;

    public NetworkEmptyClanPartyAdded(string leaderId, string[] shipIds)
    {
        LeaderId = leaderId;
        ShipIds = shipIds;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkEmptyClanPartyRemoved : ICommand
{
    [ProtoMember(1)]
    public readonly string LeaderId;

    public NetworkEmptyClanPartyRemoved(string leaderId)
    {
        LeaderId = leaderId;
    }
}
