using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Barters.Interfaces;
using GameInterface.Services.Barters.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapNotificationTypes;

namespace GameInterface.Services.Barters.Handlers;

internal class RansomOfferHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IRansomOfferCampaignBehaviorInterface ransomOfferCampaignBehaviorInterface;

    public RansomOfferHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IRansomOfferCampaignBehaviorInterface ransomOfferCampaignBehaviorInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.ransomOfferCampaignBehaviorInterface = ransomOfferCampaignBehaviorInterface;

        messageBroker.Subscribe<RansomOfferAccepted>(Handle_RansomOfferAccepted);
        messageBroker.Subscribe<NetworkRansomOfferAccepted>(Handle_NetworkRansomOfferAccepted);

        messageBroker.Subscribe<RansomOfferDeclined>(Handle_RansomOfferDeclined);
        messageBroker.Subscribe<NetworkRansomOfferDeclined>(Handle_NetworkRansomOfferDeclined);

        messageBroker.Subscribe<NetworkRansomOfferSent>(Handle_NetworkRansomOfferSent);

        messageBroker.Subscribe<NetworkRansomOfferCancelled>(Handle_NetworkRansomOfferCancelled);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RansomOfferAccepted>(Handle_RansomOfferAccepted);
        messageBroker.Unsubscribe<NetworkRansomOfferAccepted>(Handle_NetworkRansomOfferAccepted);

        messageBroker.Unsubscribe<RansomOfferDeclined>(Handle_RansomOfferDeclined);
        messageBroker.Unsubscribe<NetworkRansomOfferDeclined>(Handle_NetworkRansomOfferDeclined);

        messageBroker.Unsubscribe<NetworkRansomOfferSent>(Handle_NetworkRansomOfferSent);

        messageBroker.Unsubscribe<NetworkRansomOfferCancelled>(Handle_NetworkRansomOfferCancelled);
    }

    private void Handle_RansomOfferAccepted(MessagePayload<RansomOfferAccepted> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.RansomHero, out var ransomHeroId)) return;

        network.SendAll(new NetworkRansomOfferAccepted(mainHeroId, ransomHeroId));
    }

    private void Handle_NetworkRansomOfferAccepted(MessagePayload<NetworkRansomOfferAccepted> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, data.MainHeroId)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var mainHero)) return;

            ransomOfferCampaignBehaviorInterface.AcceptRansomOffer(data.MainHeroId, data.RansomHeroId);
        });
    }

    private void Handle_RansomOfferDeclined(MessagePayload<RansomOfferDeclined> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.RansomHero, out var ransomHeroId)) return;

        network.SendAll(new NetworkRansomOfferDeclined(mainHeroId, ransomHeroId));
    }

    private void Handle_NetworkRansomOfferDeclined(MessagePayload<NetworkRansomOfferDeclined> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, data.MainHeroId)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var mainHero)) return;

            ransomOfferCampaignBehaviorInterface.DeclineRansomOffer(data.MainHeroId, data.RansomHeroId);
        });
    }

    private void Handle_NetworkRansomOfferSent(MessagePayload<NetworkRansomOfferSent> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.PlayerHeroId, out var playerHero)) return;
            if (playerHero != Hero.MainHero) return;

            if (!objectManager.TryGetObjectWithLogging<Hero>(data.RansomHeroId, out var ransomHero)) return;

            ransomOfferCampaignBehaviorInterface.SetClientRansomPrice((int)data.RansomValue);

            // Create ransom offer notification
            StringHelpers.SetCharacterProperties("CAPTIVE_HERO", ransomHero.CharacterObject, RansomOfferCampaignBehavior.RansomOfferDescriptionText, false);
            Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(new RansomOfferMapNotification(ransomHero, RansomOfferCampaignBehavior.RansomOfferDescriptionText));
        });
    }

    private void Handle_NetworkRansomOfferCancelled(MessagePayload<NetworkRansomOfferCancelled> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.PlayerHeroId, out var playerHero)) return;
            if (playerHero != Hero.MainHero) return;

            if (!objectManager.TryGetObjectWithLogging<Hero>(data.CurrentRansomHeroId, out var ransomHero)) return;

            // Closes ransom offer notification
            CampaignEventDispatcher.Instance.OnRansomOfferCancelled(ransomHero);
        });
    }
}
