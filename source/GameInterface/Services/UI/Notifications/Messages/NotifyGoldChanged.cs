using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyGoldChanged : IEvent
{
    public readonly Hero GiverHero;
    public readonly PartyBase GiverParty;
    public readonly Hero RecipientHero;
    public readonly PartyBase RecipientParty;
    public readonly int GoldAmount;
    public readonly bool ShowQuickinformation;

    public NotifyGoldChanged(
        Hero giverHero,
        PartyBase giverParty,
        Hero recipientHero,
        PartyBase recipientParty,
        int goldAmount,
        bool showQuickinformation)
    {
        GiverHero = giverHero;
        GiverParty = giverParty;
        RecipientHero = recipientHero;
        RecipientParty = recipientParty;
        GoldAmount = goldAmount;
        ShowQuickinformation = showQuickinformation;
    }
}
