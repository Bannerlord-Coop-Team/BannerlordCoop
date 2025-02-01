using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages.Lifetime;

/// <summary>
/// Event that is published when a party is created on the server.
/// </summary>
public record PartyCreated : IEvent
{
    public MobileParty Instance { get; }

    public PartyCreated(MobileParty instance)
    {
        Instance = instance;
    }
}
