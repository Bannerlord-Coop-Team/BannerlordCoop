using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

/// <summary>
/// Local request to open a player encounter conversation. Clients request server approval, while the server uses the
/// same event when its AI detects an encounter involving a client party.
/// </summary>
internal readonly struct ConversationRequested : IEvent
{
    public readonly PartyBase DefenderParty;
    public readonly PartyBase AttackerParty;
    public readonly bool ForcePlayerOutFromSettlement;
    public readonly ConversationRestartSource Source;
    public readonly bool ArmyTalkEncounter;

    public ConversationRequested(PartyBase defenderParty, PartyBase attackerParty, bool forcePlayerOutFromSettlement, ConversationRestartSource source, bool armyTalkEncounter)
    {
        DefenderParty = defenderParty;
        AttackerParty = attackerParty;
        ForcePlayerOutFromSettlement = forcePlayerOutFromSettlement;
        Source = source;
        ArmyTalkEncounter = armyTalkEncounter;
    }
}
