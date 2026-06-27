using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages.Lifetime;

/// <summary>
/// [Client local] A player destroyed a party directly (e.g. recruiting surrendering bandits via
/// dialogue) on the conversing client. The local destroy is suppressed and this is published so the
/// server can apply the destruction authoritatively, which then replicates to every peer through
/// <see cref="NetworkApplyDestroyParty"/>.
/// </summary>
internal readonly struct DestroyPartyRequested : IEvent
{
    public readonly PartyBase DestroyerParty;
    public readonly MobileParty DefeatedParty;

    public DestroyPartyRequested(PartyBase destroyerParty, MobileParty defeatedParty)
    {
        DestroyerParty = destroyerParty;
        DefeatedParty = defeatedParty;
    }
}
