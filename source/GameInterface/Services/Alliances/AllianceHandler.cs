using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Alliances.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Alliances;

public class AllianceHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    public AllianceHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<AllianceStarted>(Handle_AllianceStarted);
        messageBroker.Subscribe<NetworkAllianceStarted>(Handle_NetworkAllianceStarted);
        messageBroker.Subscribe<AllianceEnded>(Handle_AllianceEnded);
        messageBroker.Subscribe<NetworkAllianceEnded>(Handle_NetworkAllianceEnded);
        messageBroker.Subscribe<CallToWarAgreementStarted>(Handle_CallToWarAgreementStarted);
        messageBroker.Subscribe<NetworkCallToWarAgreementStarted>(Handle_NetworkCallToWarAgreementStarted);
        messageBroker.Subscribe<CallToWarAgreementEnded>(Handle_CallToWarAgreementEnded);
        messageBroker.Subscribe<NetworkCallToWarAgreementEnded>(Handle_NetworkCallToWarAgreementEnded);
        messageBroker.Subscribe<AllianceAcceptRequested>(Handle_AllianceAcceptRequested);
        messageBroker.Subscribe<NetworkRequestStartAlliance>(Handle_NetworkRequestStartAlliance);
        messageBroker.Subscribe<CallToWarAcceptRequested>(Handle_CallToWarAcceptRequested);
        messageBroker.Subscribe<NetworkRequestStartCallToWarAgreement>(Handle_NetworkRequestStartCallToWarAgreement);
        messageBroker.Subscribe<CallToWarOfferDenied>(Handle_CallToWarOfferDenied);
        messageBroker.Subscribe<NetworkCallToWarOfferDenied>(Handle_NetworkCallToWarOfferDenied);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AllianceStarted>(Handle_AllianceStarted);
        messageBroker.Unsubscribe<NetworkAllianceStarted>(Handle_NetworkAllianceStarted);
        messageBroker.Unsubscribe<AllianceEnded>(Handle_AllianceEnded);
        messageBroker.Unsubscribe<NetworkAllianceEnded>(Handle_NetworkAllianceEnded);
        messageBroker.Unsubscribe<CallToWarAgreementStarted>(Handle_CallToWarAgreementStarted);
        messageBroker.Unsubscribe<NetworkCallToWarAgreementStarted>(Handle_NetworkCallToWarAgreementStarted);
        messageBroker.Unsubscribe<CallToWarAgreementEnded>(Handle_CallToWarAgreementEnded);
        messageBroker.Unsubscribe<NetworkCallToWarAgreementEnded>(Handle_NetworkCallToWarAgreementEnded);
        messageBroker.Unsubscribe<AllianceAcceptRequested>(Handle_AllianceAcceptRequested);
        messageBroker.Unsubscribe<NetworkRequestStartAlliance>(Handle_NetworkRequestStartAlliance);
        messageBroker.Unsubscribe<CallToWarAcceptRequested>(Handle_CallToWarAcceptRequested);
        messageBroker.Unsubscribe<NetworkRequestStartCallToWarAgreement>(Handle_NetworkRequestStartCallToWarAgreement);
        messageBroker.Unsubscribe<CallToWarOfferDenied>(Handle_CallToWarOfferDenied);
        messageBroker.Unsubscribe<NetworkCallToWarOfferDenied>(Handle_NetworkCallToWarOfferDenied);
    }
    private void Handle_AllianceStarted(MessagePayload<AllianceStarted> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetIdWithLogging(obj.ProposerKingdom, out var proposerKingdomId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.ReceiverKingdom, out var receiverKingdomId)) return;

        network.SendAll(new NetworkAllianceStarted(proposerKingdomId, receiverKingdomId));
    }

    private void Handle_NetworkAllianceStarted(MessagePayload<NetworkAllianceStarted> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.ProposerKingdomId, out var proposerKingdom)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.ReceiverKingdomId, out var receiverKingdom)) return;

            AllianceCampaignBehavior behavior = Campaign.Current.GetCampaignBehavior<AllianceCampaignBehavior>();
            if (behavior != null && !behavior.IsAllyWithKingdom(proposerKingdom, receiverKingdom))
            {
                behavior.AddAlliance(proposerKingdom, receiverKingdom);
            }
        });
    }
    private void Handle_AllianceEnded(MessagePayload<AllianceEnded> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetIdWithLogging(obj.Kingdom1, out var kingdom1Id)) return;
        if (!objectManager.TryGetIdWithLogging(obj.Kingdom2, out var kingdom2Id)) return;

        network.SendAll(new NetworkAllianceEnded(kingdom1Id, kingdom2Id));
    }

    private void Handle_NetworkAllianceEnded(MessagePayload<NetworkAllianceEnded> payload)
    {
        var obj = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.Kingdom1Id, out var kingdom1)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.Kingdom2Id, out var kingdom2)) return;

            var behavior = Campaign.Current.GetCampaignBehavior<AllianceCampaignBehavior>();
            if (behavior == null) return;

            behavior.RemoveAlliance(kingdom1, kingdom2);
        });
    }

    private void Handle_CallToWarAgreementStarted(MessagePayload<CallToWarAgreementStarted> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetIdWithLogging(obj.CallingKingdom, out var callingId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.CalledKingdom, out var calledId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.KingdomToCallToWarAgainst, out var targetId)) return;

        network.SendAll(new NetworkCallToWarAgreementStarted(callingId, calledId, targetId));
    }

    private void Handle_NetworkCallToWarAgreementStarted(MessagePayload<NetworkCallToWarAgreementStarted> payload)
    {
        var obj = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.CallingKingdomId, out var callingKingdom)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.CalledKingdomId, out var calledKingdom)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.KingdomToCallToWarAgainstId, out var targetKingdom)) return;

            var behavior = Campaign.Current.GetCampaignBehavior<AllianceCampaignBehavior>();
            if (behavior == null) return;

            var agreement = behavior.AddCallToWarAgreement(callingKingdom, calledKingdom, targetKingdom);
            behavior.UpdateAllianceEndTime(callingKingdom, calledKingdom, agreement.EndTime);
        });
    }

    private void Handle_CallToWarAgreementEnded(MessagePayload<CallToWarAgreementEnded> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetIdWithLogging(obj.CallingKingdom, out var callingId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.CalledKingdom, out var calledId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.KingdomToCallToWarAgainst, out var targetId)) return;

        network.SendAll(new NetworkCallToWarAgreementEnded(callingId, calledId, targetId));
    }

    private void Handle_NetworkCallToWarAgreementEnded(MessagePayload<NetworkCallToWarAgreementEnded> payload)
    {
        var obj = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.CallingKingdomId, out var callingKingdom)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.CalledKingdomId, out var calledKingdom)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.KingdomToCallToWarAgainstId, out var targetKingdom)) return;

            var behavior = Campaign.Current.GetCampaignBehavior<AllianceCampaignBehavior>();

            if (behavior == null) return;
            behavior.RemoveCallToWarAgreement(callingKingdom, calledKingdom, targetKingdom);
        });
    }

    private void Handle_AllianceAcceptRequested(MessagePayload<AllianceAcceptRequested> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetIdWithLogging(obj.ProposerKingdom, out var proposerId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.ReceiverKingdom, out var receiverId)) return;

        network.SendAll(new NetworkRequestStartAlliance(proposerId, receiverId));
    }

    private void Handle_NetworkRequestStartAlliance(MessagePayload<NetworkRequestStartAlliance> payload)
    {
        if (ModInformation.IsClient) return; 

        var obj = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.ProposerKingdomId, out var proposer)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.ReceiverKingdomId, out var receiver)) return;

            var behavior = Campaign.Current.GetCampaignBehavior<AllianceCampaignBehavior>();
            behavior?.StartAlliance(proposer, receiver);
        });
    }

    private void Handle_CallToWarAcceptRequested(MessagePayload<CallToWarAcceptRequested> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetIdWithLogging(obj.CallingKingdom, out var callingId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.CalledKingdom, out var calledId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.KingdomToCallToWarAgainst, out var targetId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.Player, out var playerId)) return;

        network.SendAll(new NetworkRequestStartCallToWarAgreement(callingId, calledId, targetId, playerId, obj.IsPlayerPaying));
    }

    private void Handle_NetworkRequestStartCallToWarAgreement(MessagePayload<NetworkRequestStartCallToWarAgreement> payload)
    {
        if (ModInformation.IsClient) return;

        var obj = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.CallingKingdomId, out var calling)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.CalledKingdomId, out var called)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.KingdomToCallToWarAgainstId, out var target)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.PlayerId, out var player)) return;

            int actualCost = Campaign.Current.Models.AllianceModel.GetCallToWarCost(calling, called, target);
            var behavior = Campaign.Current.GetCampaignBehavior<AllianceCampaignBehavior>();
            if (obj.IsPlayerPaying)
            {
                try
                {
                    AllianceCampaignBehaviorPatches.PendingPayingHero = player;
                    behavior?.StartCallToWarAgreement(calling, called, target, actualCost, isPlayerPaying: true);
                }
                finally
                {
                    AllianceCampaignBehaviorPatches.PendingPayingHero = null;
                }
            }
            else
            {
                behavior?.StartCallToWarAgreement(calling, called, target, actualCost, isPlayerPaying: false);
            }
        });
    }

    private void Handle_CallToWarOfferDenied(MessagePayload<CallToWarOfferDenied> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetIdWithLogging(obj.CallingKingdom, out var callingId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.CalledKingdom, out var calledId)) return;

        network.SendAll(new NetworkCallToWarOfferDenied(callingId, calledId));
    }

    private void Handle_NetworkCallToWarOfferDenied(MessagePayload<NetworkCallToWarOfferDenied> payload)
    {
        var obj = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.CallingKingdomId, out var callingKingdom)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.CalledKingdomId, out var calledKingdom)) return;

            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(calledKingdom.Leader, callingKingdom.Leader, -50, true);
        });
    }
}
