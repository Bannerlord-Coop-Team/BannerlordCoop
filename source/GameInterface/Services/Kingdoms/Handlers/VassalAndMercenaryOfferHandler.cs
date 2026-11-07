using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Kingdoms.Interfaces;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Kingdoms.Handlers;

internal class VassalAndMercenaryOfferHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IVassalAndMercenaryOfferCampaignBehaviorInterface vassalAndMercenaryOfferCampaignBehaviorInterface;

    public VassalAndMercenaryOfferHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IVassalAndMercenaryOfferCampaignBehaviorInterface vassalAndMercenaryOfferCampaignBehaviorInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.vassalAndMercenaryOfferCampaignBehaviorInterface = vassalAndMercenaryOfferCampaignBehaviorInterface;

        messageBroker.Subscribe<MercenaryOfferAccepted>(Handle_MercenaryOfferAccepted);
        messageBroker.Subscribe<NetworkMercenaryOfferAccepted>(Handle_NetworkMercenaryOfferAccepted);

        messageBroker.Subscribe<VassalOrMercenaryOfferDeclined>(Handle_VassalOrMercenaryOfferDeclined);
        messageBroker.Subscribe<NetworkVassalOrMercenaryOfferDeclined>(Handle_NetworkVassalOrMercenaryOfferDeclined);

        messageBroker.Subscribe<NetworkMercenaryOfferSent>(Handle_NetworkMercenaryOfferSent);
        messageBroker.Subscribe<NetworkVassalOfferSent>(Handle_NetworkVassalOfferSent);

        messageBroker.Subscribe<NetworkVassalOrMercenaryOfferCancelled>(Handle_NetworkVassalOrMercenaryOfferCancelled);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MercenaryOfferAccepted>(Handle_MercenaryOfferAccepted);
        messageBroker.Unsubscribe<NetworkMercenaryOfferAccepted>(Handle_NetworkMercenaryOfferAccepted);

        messageBroker.Unsubscribe<VassalOrMercenaryOfferDeclined>(Handle_VassalOrMercenaryOfferDeclined);
        messageBroker.Unsubscribe<NetworkVassalOrMercenaryOfferDeclined>(Handle_NetworkVassalOrMercenaryOfferDeclined);

        messageBroker.Unsubscribe<NetworkMercenaryOfferSent>(Handle_NetworkMercenaryOfferSent);
        messageBroker.Unsubscribe<NetworkVassalOfferSent>(Handle_NetworkVassalOfferSent);

        messageBroker.Unsubscribe<NetworkVassalOrMercenaryOfferCancelled>(Handle_NetworkVassalOrMercenaryOfferCancelled);
    }

    private void Handle_MercenaryOfferAccepted(MessagePayload<MercenaryOfferAccepted> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Kingdom, out var kingdomId)) return;

        network.SendAll(new NetworkMercenaryOfferAccepted(mainHeroId, kingdomId));
    }

    private void Handle_NetworkMercenaryOfferAccepted(MessagePayload<NetworkMercenaryOfferAccepted> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, data.MainHeroId)) return;

            vassalAndMercenaryOfferCampaignBehaviorInterface.AcceptMercenaryOffer(data.MainHeroId, data.KingdomId);
        });
    }

    private void Handle_VassalOrMercenaryOfferDeclined(MessagePayload<VassalOrMercenaryOfferDeclined> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Kingdom, out var kingdomId)) return;

        network.SendAll(new NetworkVassalOrMercenaryOfferDeclined(mainHeroId, kingdomId));
    }

    private void Handle_NetworkVassalOrMercenaryOfferDeclined(MessagePayload<NetworkVassalOrMercenaryOfferDeclined> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, data.MainHeroId)) return;

            vassalAndMercenaryOfferCampaignBehaviorInterface.DeclineOffer(data.MainHeroId, data.KingdomId);
        });
    }

    private void Handle_NetworkMercenaryOfferSent(MessagePayload<NetworkMercenaryOfferSent> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.PlayerHeroId, out var playerHero)) return;
            if (playerHero != Hero.MainHero) return;

            if (!objectManager.TryGetObjectWithLogging<Kingdom>(data.KingdomId, out var kingdom)) return;
            if (!TryGetVassalAndMercenaryOfferBehavior(out var behavior)) return;

            if (behavior._currentMercenaryOffer?.Item1 == kingdom) return;

            // Tracks the offer locally and creates the offer notification
            behavior.CreateMercenaryOffer(kingdom);
        });
    }

    private void Handle_NetworkVassalOfferSent(MessagePayload<NetworkVassalOfferSent> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.PlayerHeroId, out var playerHero)) return;
            if (playerHero != Hero.MainHero) return;

            if (!objectManager.TryGetObjectWithLogging<Kingdom>(data.KingdomId, out var kingdom)) return;
            if (!TryGetVassalAndMercenaryOfferBehavior(out var behavior)) return;

            if (behavior._vassalOffers.ContainsKey(kingdom)) return;

            // Tracks the offer locally for the vassal dialogues and creates the offer notification
            behavior.CreateVassalOffer(kingdom);
        });
    }

    private void Handle_NetworkVassalOrMercenaryOfferCancelled(MessagePayload<NetworkVassalOrMercenaryOfferCancelled> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.PlayerHeroId, out var playerHero)) return;
            if (playerHero != Hero.MainHero) return;

            if (!objectManager.TryGetObjectWithLogging<Kingdom>(data.KingdomId, out var kingdom)) return;
            if (!TryGetVassalAndMercenaryOfferBehavior(out var behavior)) return;

            // Clear before firing the event so the closing notification doesn't send a decline back
            behavior.ClearKingdomOffer(kingdom);
            CampaignEventDispatcher.Instance.OnVassalOrMercenaryServiceOfferCanceled(kingdom);
        });
    }

    private bool TryGetVassalAndMercenaryOfferBehavior(out VassalAndMercenaryOfferCampaignBehavior behavior)
    {
        behavior = Campaign.Current?.GetCampaignBehavior<VassalAndMercenaryOfferCampaignBehavior>();
        return behavior != null;
    }
}
