using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyKingdomInfluenceChanged : IEvent
{
    public readonly Hero Hero;
    public readonly MobileParty MobileParty;
    public readonly Clan Clan;
    public readonly int GainedInfluence;
    public readonly GainKingdomInfluenceAction.InfluenceGainingReason Detail;

    public NotifyKingdomInfluenceChanged(
        Hero hero,
        MobileParty mobileParty,
        Clan clan,
        int gainedInfluence,
        GainKingdomInfluenceAction.InfluenceGainingReason detail)
    {
        Hero = hero;
        MobileParty = mobileParty;
        Clan = clan;
        GainedInfluence = gainedInfluence;
        Detail = detail;
    }
}
