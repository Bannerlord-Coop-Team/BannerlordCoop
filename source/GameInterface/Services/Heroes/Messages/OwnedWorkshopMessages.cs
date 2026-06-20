using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Heroes.Messages;

public readonly struct OwnedWorkshopAdded : IEvent
{
    public readonly Hero Hero;
    public readonly Workshop Workshop;

    public OwnedWorkshopAdded(Hero hero, Workshop workshop)
    {
        Hero = hero;
        Workshop = workshop;
    }
}

public readonly struct OwnedWorkshopRemoved : IEvent
{
    public readonly Hero Hero;
    public readonly Workshop Workshop;

    public OwnedWorkshopRemoved(Hero hero, Workshop workshop)
    {
        Hero = hero;
        Workshop = workshop;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct AddOwnedWorkshop : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string WorkshopId;

    public AddOwnedWorkshop(string heroId, string workshopId)
    {
        HeroId = heroId;
        WorkshopId = workshopId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct RemoveOwnedWorkshop : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string WorkshopId;

    public RemoveOwnedWorkshop(string heroId, string workshopId)
    {
        HeroId = heroId;
        WorkshopId = workshopId;
    }
}