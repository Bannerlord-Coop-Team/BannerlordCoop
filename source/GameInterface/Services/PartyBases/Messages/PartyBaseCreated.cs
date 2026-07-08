using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyBases.Messages;

/// <summary>
/// Server-side: a <see cref="PartyBase"/> was constructed. Carries the owning
/// <see cref="MobileParty"/> constructor argument so clients can ADOPT the PartyBase they
/// already built for that party instead of receiving a skip-constructed duplicate shell
/// (see <see cref="PartyBaseLifetimeHandler"/>).
/// </summary>
internal readonly struct PartyBaseCreated : IEvent
{
    public readonly PartyBase Instance;
    public readonly MobileParty OwnerParty;

    public PartyBaseCreated(PartyBase instance, MobileParty ownerParty)
    {
        Instance = instance;
        OwnerParty = ownerParty;
    }
}
