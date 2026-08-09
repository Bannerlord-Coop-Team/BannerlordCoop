using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Clans.Messages;

public readonly struct OnClanChangedKingdom : IEvent
{
    public readonly Clan Clan;
    public readonly Kingdom OldKingdom;
    public readonly Kingdom NewKingdom;
    public readonly ChangeKingdomAction.ChangeKingdomActionDetail Detail;

    public OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail)
    {
        Clan = clan;
        OldKingdom = oldKingdom;
        NewKingdom = newKingdom;
        Detail = detail;
    }
}
