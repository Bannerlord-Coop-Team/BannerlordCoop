using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobileParties.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct RemoveVolunteer : ICommand
{
    [ProtoMember(1)]
    public readonly string IndividualId;

    [ProtoMember(2)]
    public readonly int BitCode;

    public RemoveVolunteer(string individualId, int bitCode)
    {
        IndividualId = individualId;
        BitCode = bitCode;
    }
}