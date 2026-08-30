using Autofac;
using Common;
using Common.Util;
using GameInterface.Services.Kingdoms.Commands;
using GameInterface.Services.ObjectManager;
using Moq;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
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
