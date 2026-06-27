using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Messages;

internal readonly struct CaravanPartyOwnerChanged : IEvent
{
    public readonly CaravanPartyComponent Instance;
    public readonly Hero Owner;

    public CaravanPartyOwnerChanged(CaravanPartyComponent instance, Hero owner)
    {
        Instance = instance;
        Owner = owner;
    }
}
