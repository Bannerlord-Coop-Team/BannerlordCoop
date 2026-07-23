using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Actions.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct ChangeWorkshopOwner : ICommand
{
    [ProtoMember(1)]
    public readonly string WorkshopId;

    [ProtoMember(2)]
    public readonly string NewOwnerId;

    [ProtoMember(3)]
    public readonly string WorkshopTypeId;

    [ProtoMember(4)]
    public readonly int Capital;

    [ProtoMember(5)]
    public readonly int Cost;

    [ProtoMember(6)]
    public readonly string ExpectedOwnerId;

    public ChangeWorkshopOwner(string workshopId, string expectedOwnerId, string newOwnerId, string workshopTypeId, int capital, int cost)
    {
        WorkshopId = workshopId;
        ExpectedOwnerId = expectedOwnerId;
        NewOwnerId = newOwnerId;
        WorkshopTypeId = workshopTypeId;
        Capital = capital;
        Cost = cost;
    }
}
