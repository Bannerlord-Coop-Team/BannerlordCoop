using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

public readonly struct PrisonersSold : IEvent
{
    public readonly PartyBase SellingParty;
    public readonly TroopRoster LeftPrisonerRoster;

    public PrisonersSold(
        PartyBase sellingParty,
        TroopRoster leftPrisonerRoster)
    {
        SellingParty = sellingParty;
        LeftPrisonerRoster = leftPrisonerRoster;
    }
}