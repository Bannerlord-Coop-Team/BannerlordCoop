using Common.Messaging;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.BanditPartyComponents.Messages;

internal readonly struct BanditPartyComponentInitArgsUpdated : IEvent
{
    public readonly BanditPartyComponent Instance;
    public readonly BanditPartyComponent.InitializationArgs InitArgs;

    public BanditPartyComponentInitArgsUpdated(BanditPartyComponent instance, BanditPartyComponent.InitializationArgs initArgs)
    {
        Instance = instance;
        InitArgs = initArgs;
    }
}
