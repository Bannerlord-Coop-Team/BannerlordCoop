using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record VolunteerTypesArrayUpdated(Hero Instance, CharacterObject Value, int Index) : IEvent
{
    public Hero Instance { get; } = Instance;
    public CharacterObject Value { get; } = Value;
    public int Index { get; } = Index;
}
