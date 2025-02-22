using Common.Messaging;
using GameInterface.Utils;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record VolunteerTypesArrayUpdated : GenericArrayEvent<Hero, CharacterObject>
{
    public VolunteerTypesArrayUpdated(Hero instance, CharacterObject value, int index) : base(instance, value, index)
    {
        Instance = instance;
        Value = value;
        Index = index;
    }

    public Hero Instance { get; }
    public CharacterObject Value { get; }
    public int Index { get; }
}
