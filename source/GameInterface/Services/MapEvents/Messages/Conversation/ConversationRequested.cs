using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

/// <summary>
/// Local (client-side) request, published by the <c>PlayerEncounter.RestartPlayerEncounter</c> prefix, asking the
/// server whether this encounter restart is allowed to run. Bridged to the network (and rate-limited) by
/// <see cref="Handlers.ConversationRequestHandler"/>.
/// </summary>
internal readonly struct ConversationRequested : IEvent
{
    public readonly PartyBase DefenderParty;
    public readonly PartyBase AttackerParty;
    public readonly bool ForcePlayerOutFromSettlement;
    public readonly ConversationRestartSource Source;

    public ConversationRequested(PartyBase defenderParty, PartyBase attackerParty, bool forcePlayerOutFromSettlement, ConversationRestartSource source)
    {
        DefenderParty = defenderParty;
        AttackerParty = attackerParty;
        ForcePlayerOutFromSettlement = forcePlayerOutFromSettlement;
        Source = source;
    }
}
