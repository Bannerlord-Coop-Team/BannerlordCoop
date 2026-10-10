using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.StanceLinks.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkStanceLinkSuccessfulSieges2 : ICommand
{
    [ProtoMember(1)]
    public readonly string StanceLinkId;
    [ProtoMember(2)]
    public readonly int Value;

    public NetworkStanceLinkSuccessfulSieges2(string stanceLinkId, int value)
    {
        StanceLinkId = stanceLinkId;
        Value = value;
    }
}
