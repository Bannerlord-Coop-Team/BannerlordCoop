using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

internal readonly struct ConversationRestartApproved : IEvent
{
    public readonly PartyBase Defender;
    public readonly PartyBase Attacker;
    public readonly string RequestId;

    public ConversationRestartApproved(PartyBase defender, PartyBase attacker, string requestId)
    {
        Defender = defender;
        Attacker = attacker;
        RequestId = requestId;
    }
}

internal readonly struct ConversationRestartRejected : IEvent
{
    public readonly string RequestId;

    public ConversationRestartRejected(string requestId) => RequestId = requestId;
}
