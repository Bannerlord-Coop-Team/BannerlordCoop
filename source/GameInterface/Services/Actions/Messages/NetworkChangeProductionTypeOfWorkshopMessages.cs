using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Actions.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct ChangeProductionTypeOfWorkshop : ICommand
{
    [ProtoMember(1)]
    public readonly string WorkshopId;

    [ProtoMember(2)]
    public readonly string WorkshopTypeId;

    public ChangeProductionTypeOfWorkshop(
        string workshopId,
        string workshopTypeId)
    {
        WorkshopId = workshopId;
        WorkshopTypeId = workshopTypeId;
    }
}
