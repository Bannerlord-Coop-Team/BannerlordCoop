using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages.Lifetime;

/// <summary>
/// Event that is published when a party is destroyed on the server.
/// </summary>
public record PartyDestroyed : IEvent
{
    public MobileParty Instance { get; }

    public PartyDestroyed(MobileParty instance)
    {
        Instance = instance;
    }
}
