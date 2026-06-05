using Common.Logging;
using Common.Messaging;
using GameInterface.Services.UI.Notifications.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.UI.Notifications.Handlers;

internal class GoldNotificationHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<GoldNotificationHandler>();

    private readonly IMessageBroker messageBroker;

    public GoldNotificationHandler(
        IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<NotifyGoldChange>(Handle_NotifyGoldChange);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NotifyGoldChange>(Handle_NotifyGoldChange);
    }

    private void Handle_NotifyGoldChange(MessagePayload<NotifyGoldChange> obj)
    {
        CampaignEventDispatcher.Instance.OnHeroOrPartyTradedGold(
            new ValueTuple<Hero, PartyBase>(null, null),
            new ValueTuple<Hero, PartyBase>(Hero.MainHero, MobileParty.MainParty.Party),
            new ValueTuple<int, string>(obj.What.GoldAmount, ""), true);
    }
}