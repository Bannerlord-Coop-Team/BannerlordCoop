using Autofac;
using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MobilePartyAIs.Messages;
using GameInterface.Services.MobilePartyAIs.Patches;
using Moq;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobilePartyAIs;

/// <summary>
/// Tests synchronization messages emitted by the <see cref="MobilePartyAi.AiBehaviorInteractable"/> setter patch.
/// </summary>
[Collection(nameof(MobilePartyAiSyncCollection))]
public class MobilePartyAIPatchesTests
{
    [Fact]
    public void SameInteractable_DoesNotPublishUpdate()
    {
        var interactablePoint = ObjectHelper.SkipConstructor<PartyBase>();
        var partyAi = CreatePartyAi(interactablePoint);

        var published = RunPrefix(partyAi, interactablePoint);

        Assert.Empty(published);
    }

    [Fact]
    public void RepeatedNull_DoesNotPublishUpdate()
    {
        var partyAi = CreatePartyAi(null!);

        var published = RunPrefix(partyAi, null!);

        Assert.Empty(published);
    }

    [Fact]
    public void ChangedInteractable_PublishesUpdate()
    {
        var partyAi = CreatePartyAi(ObjectHelper.SkipConstructor<PartyBase>());
        var interactablePoint = ObjectHelper.SkipConstructor<PartyBase>();

        var published = RunPrefix(partyAi, interactablePoint);

        var update = Assert.Single(published);
        Assert.Same(partyAi, update.PartyAi);
        Assert.Same(interactablePoint, update.InteractablePoint);
    }

    private static MobilePartyAi CreatePartyAi(IInteractablePoint interactablePoint)
    {
        var partyAi = ObjectHelper.SkipConstructor<MobilePartyAi>();
        using (new AllowedThread())
        {
            partyAi.AiBehaviorInteractable = interactablePoint;
        }
        return partyAi;
    }

    private static List<AiBehaviorInteractablePointUpdated> RunPrefix(
        MobilePartyAi partyAi,
        IInteractablePoint interactablePoint)
    {
        var syncPolicy = new Mock<ISyncPolicy>();
        syncPolicy.Setup(policy => policy.AllowOriginal()).Returns(false);

        var builder = new ContainerBuilder();
        builder.RegisterInstance(syncPolicy.Object).As<ISyncPolicy>();
        using var container = builder.Build();

        var published = new List<AiBehaviorInteractablePointUpdated>();
        Action<MessagePayload<AiBehaviorInteractablePointUpdated>> capture = payload => published.Add(payload.What);
        bool wasServer = ModInformation.IsServer;
        ContainerProvider.SetContainer(container);
        MessageBroker.Instance.Subscribe(capture);
        try
        {
            ModInformation.IsServer = true;
            MobilePartyAIPatches.AiBehaviorInteractable_Prefix(ref partyAi, ref interactablePoint);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
            MessageBroker.Instance.Unsubscribe(capture);
            ContainerProvider.Clear();
        }

        return published;
    }
}
