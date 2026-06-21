using Common.Messaging;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Messages;

internal readonly struct LordPartyComponentInitArgsUpdated : IEvent
{
    public readonly LordPartyComponent Instance;
    public readonly LordPartyComponent.InitializationArgs InitArgs;

    public LordPartyComponentInitArgsUpdated(LordPartyComponent instance, LordPartyComponent.InitializationArgs initArgs)
    {
        Instance = instance;
        InitArgs = initArgs;
    }
}
