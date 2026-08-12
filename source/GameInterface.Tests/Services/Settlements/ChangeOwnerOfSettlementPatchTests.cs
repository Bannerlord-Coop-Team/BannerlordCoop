using Autofac;
using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Settlements.Messages;
using GameInterface.Services.Settlements.Patches;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Services.Settlements;

[Collection(ModInformationRoleCollection.Name)]
public class ChangeOwnerOfSettlementPatchTests
{
    [Fact]
    public void RebellionOwnership_Server_PublishesCompletedClanBeforeOwnershipChange()
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(new TestSyncPolicy()).As<ISyncPolicy>();
        using var container = builder.Build();

        var clan = ObjectHelper.SkipConstructor<Clan>();
        var newOwner = ObjectHelper.SkipConstructor<Hero>();
        newOwner._clan = clan;
        newOwner.StringId = "hero-1";
        var previousOwner = ObjectHelper.SkipConstructor<Hero>();
        previousOwner.StringId = "hero-old";
        var previousOwnerClan = ObjectHelper.SkipConstructor<Clan>();
        previousOwner._clan = previousOwnerClan;
        previousOwnerClan._leader = previousOwner;
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        settlement.StringId = "town-1";
        settlement.Town = ObjectHelper.SkipConstructor<Town>();
        settlement.Town._ownerClan = previousOwnerClan;

        var publishedTypes = new List<Type>();
        SettlementOwnershipChanged ownershipChanged = null!;
        Action<MessagePayload<SettlementRebelClanInitialized>> clanCapture = payload => publishedTypes.Add(payload.What.GetType());
        Action<MessagePayload<SettlementOwnershipChanged>> ownershipCapture = payload =>
        {
            publishedTypes.Add(payload.What.GetType());
            ownershipChanged = payload.What;
        };
        MessageBroker.Instance.Subscribe(clanCapture);
        MessageBroker.Instance.Subscribe(ownershipCapture);

        bool wasServer = ModInformation.IsServer;
        bool hadPreviousContainer = ContainerProvider.TryGetContainer(out var previousContainer);
        try
        {
            ModInformation.IsServer = true;
            using (ContainerProvider.UseContainerThreadSafe(container))
            {
                bool runOriginal = ChangeOwnerOfSettlementPatch.Prefix(
                    settlement,
                    newOwner,
                    null,
                    ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByRebellion);

                Assert.True(runOriginal);
            }
        }
        finally
        {
            ModInformation.IsServer = wasServer;
            MessageBroker.Instance.Unsubscribe(clanCapture);
            MessageBroker.Instance.Unsubscribe(ownershipCapture);

            if (hadPreviousContainer)
            {
                ContainerProvider.SetContainer(previousContainer);
            }
            else
            {
                ContainerProvider.Clear();
            }
        }

        Assert.Equal(
            new[] { typeof(SettlementRebelClanInitialized), typeof(SettlementOwnershipChanged) },
            publishedTypes);
        Assert.Equal(previousOwner.StringId, ownershipChanged.PreviousOwnerId);
    }

    private sealed class TestSyncPolicy : ISyncPolicy
    {
        public bool AllowOriginal() => false;
    }
}
