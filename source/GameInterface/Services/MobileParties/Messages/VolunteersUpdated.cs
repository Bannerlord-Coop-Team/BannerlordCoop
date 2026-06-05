using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MobileParties.Messages;

public readonly struct VolunteersUpdated : IEvent
{
    public readonly Dictionary<Hero, CharacterObject[]> UpdatedVolunteerTypes;

    public VolunteersUpdated(Dictionary<Hero, CharacterObject[]> updatedVolunteerTypes)
    {
        UpdatedVolunteerTypes = updatedVolunteerTypes;
    }
}