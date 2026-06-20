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

    [ProtoMember(3)]
    public readonly bool IgnoreCost;

    public ChangeProductionTypeOfWorkshop(
        string workshopId,
        string workshopTypeId,
        bool ignoreCost)
    {
        WorkshopId = workshopId;
        WorkshopTypeId = workshopTypeId;
        IgnoreCost = ignoreCost;
    }
}
