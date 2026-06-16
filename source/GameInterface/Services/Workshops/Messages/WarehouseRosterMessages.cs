using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Workshops.Messages;

public readonly struct WorkshopOwnerChanged : IEvent
{
    public readonly Workshop Workshop;
    public readonly Hero OldOwner;

    public WorkshopOwnerChanged(Workshop workshop, Hero oldOwner)
    {
        Workshop = workshop;
        OldOwner = oldOwner;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct ChangeWorkshopOwner : ICommand
{
    [ProtoMember(1)]
    public readonly string WorkshopId;

    [ProtoMember(2)]
    public readonly string OldOwnerId;

    public ChangeWorkshopOwner(string workshopId, string oldOwnerId)
    {
        WorkshopId = workshopId;
        OldOwnerId = oldOwnerId;
    }
}