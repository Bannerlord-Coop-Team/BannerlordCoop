using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.MobileParties.Messages.Behavior;
using System;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

[Collection(global::GameInterface.Tests.ModInformationRoleCollection.Name)]
public sealed class MobilePartyAiInteractablePatchTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;

    public void Dispose()
    {
        ModInformation.IsServer = wasServer;
    }

    [Fact]
    public void BareServerSetter_PublishesCompleteMovementStateTrigger()
    {
        ModInformation.IsServer = true;

        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.IsActive = true;
        var partyAi = new MobilePartyAi(party);
        party.Ai = partyAi;

        IInteractablePoint value = ObjectHelper.SkipConstructor<PartyBase>();
        MobilePartyMovementStateChanged? published = null;

        using var broker = new IsolatedMessageBroker();
        Action<MessagePayload<MobilePartyMovementStateChanged>> subscription =
            payload => published = payload.What;
        broker.Subscribe(subscription);

        bool shouldPublish = MobilePartyAIPatches.ShouldCaptureInteractableChange(partyAi, value);
        partyAi.AiBehaviorInteractable = value;
        MobilePartyAIPatches.AiBehaviorInteractable_Postfix(ref partyAi, shouldPublish);

        Assert.True(shouldPublish);
        Assert.True(published.HasValue);
        Assert.Same(party, published.Value.Party);
    }

    private sealed class IsolatedMessageBroker : MessageBroker, IDisposable
    {
        private readonly MessageBroker previous = instance;

        public IsolatedMessageBroker()
        {
            instance = this;
        }

        public void Dispose()
        {
            instance = previous;
        }
    }
}
