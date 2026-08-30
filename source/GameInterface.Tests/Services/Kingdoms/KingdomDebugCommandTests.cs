using Autofac;
using Common;
using Common.Util;
using GameInterface.Services.Kingdoms.Commands;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using Moq;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms;

[Collection(ModInformationRoleCollection.Name)]
public class KingdomDebugCommandTests
{
    [Fact]
    public void ForceAlly_WithEliminatedKingdom_ReturnsErrorWithoutFormingAlliance()
    {
        var living = ObjectHelper.SkipConstructor<Kingdom>();
        var eliminated = ObjectHelper.SkipConstructor<Kingdom>();
        eliminated._isEliminated = true;
        var objectManager = BuildObjectManager(("alive", living), ("dead", eliminated));

        var result = InvokeAsServerWithObjectManager(
            objectManager,
            () => KingdomDebugCommand.ForceAlly(new List<string> { "alive", "dead" }));

        Assert.Contains("has been eliminated", result);
    }

    [Fact]
    public void ForceTradeAgreement_WithEliminatedKingdom_ReturnsErrorWithoutFormingAgreement()
    {
        var living = ObjectHelper.SkipConstructor<Kingdom>();
        var eliminated = ObjectHelper.SkipConstructor<Kingdom>();
        eliminated._isEliminated = true;
        var objectManager = BuildObjectManager(("alive", living), ("dead", eliminated));

        var result = InvokeAsServerWithObjectManager(
            objectManager,
            () => KingdomDebugCommand.ForceTradeAgreement(new List<string> { "alive", "dead" }));

        Assert.Contains("has been eliminated", result);
    }

    [Fact]
    public void ForceTradeAgreement_WithExpiredAgreement_RenewsInsteadOfReportingExisting()
    {
        GameBootStrap.Initialize();
        var behavior = Campaign.Current.GetCampaignBehavior<TradeAgreementsCampaignBehavior>();
        Assert.NotNull(behavior);

        var kingdom1 = ObjectHelper.SkipConstructor<Kingdom>();
        var kingdom2 = ObjectHelper.SkipConstructor<Kingdom>();

        // Seed a lapsed agreement that the lazy cleanup has not yet removed.
        // CampaignTime.Now is patched to Zero in tests while IsPast still compares against the real
        // map-time ticks, so an EndTime of "now" reads as already expired.
        behavior._tradeAgreements.RemoveAll(t =>
            (t.Kingdom1 == kingdom1 && t.Kingdom2 == kingdom2) ||
            (t.Kingdom1 == kingdom2 && t.Kingdom2 == kingdom1));
        var expired = new TradeAgreementsCampaignBehavior.TradeAgreement(kingdom1, kingdom2, CampaignTime.Now);
        behavior._tradeAgreements.Add(expired);
        Assert.True(expired.EndTime.IsPast);

        var result = InvokeAsServerWithObjectManager(
            BuildObjectManager(("k1", kingdom1), ("k2", kingdom2)),
            () => KingdomDebugCommand.ForceTradeAgreement(new List<string> { "k1", "k2" }));

        // The raw-list guard would have reported the stale entry and bailed; HasTradeAgreement drops it.
        Assert.StartsWith("Forced trade agreement", result);
        Assert.True(behavior.TryGetTradeAgreement(kingdom1, kingdom2, out var index));
        Assert.True(behavior._tradeAgreements[index].EndTime.NumTicks > 0);
    }

    private static IObjectManager BuildObjectManager(params (string Id, Kingdom Kingdom)[] kingdoms)
    {
        var mock = new Mock<IObjectManager>();
        foreach (var (id, kingdom) in kingdoms)
        {
            var resolved = kingdom;
            mock.Setup(manager => manager.TryGetObject<Kingdom>(id, out resolved)).Returns(true);
        }
        return mock.Object;
    }

    private static string InvokeAsServerWithObjectManager(IObjectManager objectManager, Func<string> invoke)
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(objectManager).As<IObjectManager>();
        using var container = builder.Build();

        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        bool hadPreviousContainer = ContainerProvider.TryGetContainer(out var previousContainer);
        try
        {
            using (ContainerProvider.UseContainerThreadSafe(container))
            {
                return invoke();
            }
        }
        finally
        {
            ModInformation.IsServer = wasServer;
            if (hadPreviousContainer)
            {
                ContainerProvider.SetContainer(previousContainer);
            }
            else
            {
                ContainerProvider.Clear();
            }
        }
    }
}
