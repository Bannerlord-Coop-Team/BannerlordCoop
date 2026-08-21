using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyTributePaymentEnded : IEvent
{
    public readonly Clan Clan;
    public readonly IFaction PayerFaction;

    public NotifyTributePaymentEnded(
        Clan clan,
        IFaction payerFaction)
    {
        Clan = clan;
        PayerFaction = payerFaction;
    }
}
