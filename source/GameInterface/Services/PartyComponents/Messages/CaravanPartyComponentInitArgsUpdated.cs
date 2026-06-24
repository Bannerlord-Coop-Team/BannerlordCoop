using Common.Messaging;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Messages;

internal readonly struct CaravanPartyComponentInitArgsUpdated : IEvent
{
    public readonly CaravanPartyComponent Instance;
    public readonly CaravanPartyComponent.InitializationArgs InitArgs;

    public CaravanPartyComponentInitArgsUpdated(CaravanPartyComponent instance, CaravanPartyComponent.InitializationArgs initArgs)
    {
        Instance = instance;
        InitArgs = initArgs;
    }
}
