using Common.Messaging;
using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record VolunteerTypesArrayUpdated : GenericArrayChangedEvent<Hero, CharacterObject>
{
    public VolunteerTypesArrayUpdated(Hero instance, CharacterObject value, int index) : base(instance, value, index)
    {
    }
}
