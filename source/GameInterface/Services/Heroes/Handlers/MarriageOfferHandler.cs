using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.SceneInformationPopupTypes;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Heroes.Handlers;

internal class MarriageOfferHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IMarriageOfferCampaignBehaviorInterface marriageOfferCampaignBehaviorInterface;

    public MarriageOfferHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IMarriageOfferCampaignBehaviorInterface marriageOfferCampaignBehaviorInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.marriageOfferCampaignBehaviorInterface = marriageOfferCampaignBehaviorInterface;

        messageBroker.Subscribe<MarriageOfferAccepted>(Handle_MarriageOfferAccepted);
        messageBroker.Subscribe<NetworkMarriageOfferAccepted>(Handle_NetworkMarriageOfferAccepted);

        messageBroker.Subscribe<MarriageOfferDeclined>(Handle_MarriageOfferDeclined);
        messageBroker.Subscribe<NetworkMarriageOfferDeclined>(Handle_NetworkMarriageOfferDeclined);

        messageBroker.Subscribe<NetworkMarriageOfferSent>(Handle_NetworkMarriageOfferSent);
        messageBroker.Subscribe<NetworkMarriageOfferCanceled>(Handle_NetworkMarriageOfferCanceled);
        messageBroker.Subscribe<NetworkMarriageOfferAcceptResolved>(Handle_NetworkMarriageOfferAcceptResolved);
        messageBroker.Subscribe<NetworkMarriageEngagementEnded>(Handle_NetworkMarriageEngagementEnded);
        messageBroker.Subscribe<NetworkMarriagePreparationCanceled>(Handle_NetworkMarriagePreparationCanceled);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MarriageOfferAccepted>(Handle_MarriageOfferAccepted);
        messageBroker.Unsubscribe<NetworkMarriageOfferAccepted>(Handle_NetworkMarriageOfferAccepted);

        messageBroker.Unsubscribe<MarriageOfferDeclined>(Handle_MarriageOfferDeclined);
        messageBroker.Unsubscribe<NetworkMarriageOfferDeclined>(Handle_NetworkMarriageOfferDeclined);

        messageBroker.Unsubscribe<NetworkMarriageOfferSent>(Handle_NetworkMarriageOfferSent);
        messageBroker.Unsubscribe<NetworkMarriageOfferCanceled>(Handle_NetworkMarriageOfferCanceled);
        messageBroker.Unsubscribe<NetworkMarriageOfferAcceptResolved>(Handle_NetworkMarriageOfferAcceptResolved);
        messageBroker.Unsubscribe<NetworkMarriageEngagementEnded>(Handle_NetworkMarriageEngagementEnded);
        messageBroker.Unsubscribe<NetworkMarriagePreparationCanceled>(Handle_NetworkMarriagePreparationCanceled);
    }

    private void Handle_MarriageOfferAccepted(MessagePayload<MarriageOfferAccepted> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.PlayerClanHero, out var playerClanHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.OtherClanHero, out var otherClanHeroId)) return;

        network.SendAll(new NetworkMarriageOfferAccepted(mainHeroId, playerClanHeroId, otherClanHeroId));
    }

    private void Handle_NetworkMarriageOfferAccepted(MessagePayload<NetworkMarriageOfferAccepted> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, data.MainHeroId)) return;

            marriageOfferCampaignBehaviorInterface.AcceptMarriageOffer(data.MainHeroId, data.PlayerClanHeroId, data.OtherClanHeroId);
        });
    }

    private void Handle_MarriageOfferDeclined(MessagePayload<MarriageOfferDeclined> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.PlayerClanHero, out var playerClanHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.OtherClanHero, out var otherClanHeroId)) return;

        network.SendAll(new NetworkMarriageOfferDeclined(mainHeroId, playerClanHeroId, otherClanHeroId));
    }

    private void Handle_NetworkMarriageOfferDeclined(MessagePayload<NetworkMarriageOfferDeclined> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, data.MainHeroId)) return;

            marriageOfferCampaignBehaviorInterface.DeclineMarriageOffer(data.MainHeroId, data.PlayerClanHeroId, data.OtherClanHeroId);
        });
    }

    private void Handle_NetworkMarriageOfferSent(MessagePayload<NetworkMarriageOfferSent> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!IsLocalPlayer(data.PlayerHeroId)) return;
            if (!TryGetMarriageOfferBehavior(out var marriageOfferBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.PlayerClanHeroId, out var playerClanHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OtherClanHeroId, out var otherClanHero)) return;

            // Sets the offer the popup reads and creates the offer notification
            marriageOfferBehavior.CreateMarriageOffer(playerClanHero, otherClanHero);
        });
    }

    private void Handle_NetworkMarriageOfferCanceled(MessagePayload<NetworkMarriageOfferCanceled> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!IsLocalPlayer(data.PlayerHeroId)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.PlayerClanHeroId, out var playerClanHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OtherClanHeroId, out var otherClanHero)) return;

            // Closes the offer notification and clears the local offer
            CampaignEventDispatcher.Instance.OnMarriageOfferCanceled(
                playerClanHero.IsFemale ? otherClanHero : playerClanHero,
                playerClanHero.IsFemale ? playerClanHero : otherClanHero);
        });
    }

    private void Handle_NetworkMarriageOfferAcceptResolved(MessagePayload<NetworkMarriageOfferAcceptResolved> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!TryGetMarriageOfferBehavior(out var marriageOfferBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.PlayerClanHeroId, out var playerClanHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OtherClanHeroId, out var otherClanHero)) return;

            if (!data.Married)
            {
                // Every client tracks the engagement since the marriage model checks it in dialogue
                marriageOfferBehavior._acceptedMarriageOffersThatWaitingForAvailability[playerClanHero] = otherClanHero;

                if (!IsLocalPlayer(data.PlayerHeroId)) return;

                InformationManager.ShowInquiry(new InquiryData(string.Empty, marriageOfferBehavior.MarriagePreparationStartedText.ToString(), true, false, GameTexts.FindText("str_ok").ToString(), string.Empty, null, null));
                return;
            }

            if (!IsLocalPlayer(data.PlayerHeroId)) return;

            Hero groomHero = playerClanHero.IsFemale ? otherClanHero : playerClanHero;
            Hero brideHero = playerClanHero.IsFemale ? playerClanHero : otherClanHero;
            MBInformationManager.ShowSceneNotification(new MarriageSceneNotificationItem(groomHero, brideHero, CampaignTime.Now));
        });
    }

    private void Handle_NetworkMarriageEngagementEnded(MessagePayload<NetworkMarriageEngagementEnded> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!TryGetMarriageOfferBehavior(out var marriageOfferBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.PlayerClanHeroId, out var playerClanHero)) return;

            marriageOfferBehavior._acceptedMarriageOffersThatWaitingForAvailability.Remove(playerClanHero);
        });
    }

    private void Handle_NetworkMarriagePreparationCanceled(MessagePayload<NetworkMarriagePreparationCanceled> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!IsLocalPlayer(data.PlayerHeroId)) return;
            if (!TryGetMarriageOfferBehavior(out var marriageOfferBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.DeadHeroId, out var deadHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OtherHeroId, out var otherHero)) return;

            var canceledText = marriageOfferBehavior.MarriagePreparationCanceledText;
            StringHelpers.SetCharacterProperties("DEAD_HERO", deadHero.CharacterObject, canceledText);
            canceledText.SetTextVariable("OTHER_HERO", otherHero.Name);
            InformationManager.ShowInquiry(new InquiryData(string.Empty, canceledText.ToString(), true, false, GameTexts.FindText("str_ok").ToString(), string.Empty, null, null));
        });
    }

    private bool IsLocalPlayer(string playerHeroId)
    {
        return objectManager.TryGetObjectWithLogging<Hero>(playerHeroId, out var playerHero) && playerHero == Hero.MainHero;
    }

    private static bool TryGetMarriageOfferBehavior(out MarriageOfferCampaignBehavior marriageOfferBehavior)
    {
        marriageOfferBehavior = Campaign.Current?.GetCampaignBehavior<MarriageOfferCampaignBehavior>();
        return marriageOfferBehavior != null;
    }
}
