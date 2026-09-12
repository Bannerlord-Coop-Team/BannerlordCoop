using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Alliances.Messages;

public readonly struct AllianceAcceptRequested : IEvent
{
    public readonly Kingdom ProposerKingdom;
    public readonly Kingdom ReceiverKingdom;

    public AllianceAcceptRequested(Kingdom proposerKingdom, Kingdom receiverKingdom)
    {
        ProposerKingdom = proposerKingdom;
        ReceiverKingdom = receiverKingdom;
    }
}