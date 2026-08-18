using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Alliances.Messages;

public readonly struct AllianceStarted : IEvent
{
    public readonly Kingdom ProposerKingdom;
    public readonly Kingdom ReceiverKingdom;

    public AllianceStarted(Kingdom proposerKingdom, Kingdom receiverKingdom)
    {
        ProposerKingdom = proposerKingdom;
        ReceiverKingdom = receiverKingdom;
    }
}
