using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.UI.Notifications.Messages;
using Serilog;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.UI.Notifications.Handlers;

internal class OtherNotificationsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<OtherNotificationsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public OtherNotificationsHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<NotifyAnimalsSlaughteredToEat>(Handle_NotifyAnimalsSlaughteredToEat);
        messageBroker.Subscribe<NetworkNotifyAnimalsSlaughteredToEat>(Handle_NetworkNotifyAnimalsSlaughteredToEat);

        messageBroker.Subscribe<NotifyDailyStarvationPenalty>(Handle_NotifyDailyStarvationPenalty);
        messageBroker.Subscribe<NetworkNotifyDailyStarvationPenalty>(Handle_NetworkNotifyDailyStarvationPenalty);

        messageBroker.Subscribe<NotifyAnimalsBred>(Handle_NotifyAnimalsBred);
        messageBroker.Subscribe<NetworkNotifyAnimalsBred>(Handle_NetworkNotifyAnimalsBred);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NotifyAnimalsSlaughteredToEat>(Handle_NotifyAnimalsSlaughteredToEat);
        messageBroker.Unsubscribe<NetworkNotifyAnimalsSlaughteredToEat>(Handle_NetworkNotifyAnimalsSlaughteredToEat);

        messageBroker.Unsubscribe<NotifyDailyStarvationPenalty>(Handle_NotifyDailyStarvationPenalty);
        messageBroker.Unsubscribe<NetworkNotifyDailyStarvationPenalty>(Handle_NetworkNotifyDailyStarvationPenalty);

        messageBroker.Unsubscribe<NotifyAnimalsBred>(Handle_NotifyAnimalsBred);
        messageBroker.Unsubscribe<NetworkNotifyAnimalsBred>(Handle_NetworkNotifyAnimalsBred);
    }

    private void Handle_NotifyAnimalsSlaughteredToEat(MessagePayload<NotifyAnimalsSlaughteredToEat> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

            network.SendAll(new NetworkNotifyAnimalsSlaughteredToEat(mobilePartyId));
        });
    }

    private void Handle_NetworkNotifyAnimalsSlaughteredToEat(MessagePayload<NetworkNotifyAnimalsSlaughteredToEat> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

            if (mobileParty != MobileParty.MainParty) return;

            MBInformationManager.AddQuickInformation(new TextObject("{=WTwafRTH}Your party has slaughtered some animals to eat.", null), 0, null, null, "");
        });
    }

    private void Handle_NotifyDailyStarvationPenalty(MessagePayload<NotifyDailyStarvationPenalty> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

            network.SendAll(new NetworkNotifyDailyStarvationPenalty(mobilePartyId, obj.What.DailyStarvationMoralePenalty));
        });
    }

    private void Handle_NetworkNotifyDailyStarvationPenalty(MessagePayload<NetworkNotifyDailyStarvationPenalty> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

            if (mobileParty != MobileParty.MainParty) return;

            MBTextManager.SetTextVariable("MORALE_PENALTY", obj.What.DailyStarvationMoralePenalty);
            MBInformationManager.AddQuickInformation(new TextObject("{=qhL5o55i}Your party is starving. You lose {MORALE_PENALTY} morale.", null), 0, null, null, "");
        });
    }

    private void Handle_NotifyAnimalsBred(MessagePayload<NotifyAnimalsBred> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

            network.SendAll(new NetworkNotifyAnimalsBred(mobilePartyId, obj.What.NumberBred, obj.What.BredAnimal));
        });
    }

    private void Handle_NetworkNotifyAnimalsBred(MessagePayload<NetworkNotifyAnimalsBred> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

            if (mobileParty != MobileParty.MainParty) return;

            TextObject textObject = new TextObject("{=vl9bawa7}{COUNT} {?(COUNT > 1)}{PLURAL(ANIMAL_NAME)} are{?}{ANIMAL_NAME} is{\\?} added to your party.", null);
            textObject.SetTextVariable("COUNT", obj.What.NumberBred);
            textObject.SetTextVariable("ANIMAL_NAME", obj.What.BredAnimal.EquipmentElement.Item.Name);
            InformationManager.DisplayMessage(new InformationMessage(textObject.ToString()));
        });
    }
}