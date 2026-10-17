using Autofac;
using Common.Util;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEvents.Patches;
using GameInterface.Tests.Bootstrap;
using Moq;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

/// <summary>Verifies the stale siege encounter menu routes a held capture choice to the aftermath menu.</summary>
[Collection(nameof(CampaignCurrentCollection))]
public class EncounterCaptureAftermathInitPatchTests
{
    [Fact]
    public void FinishedEncounter_WithHeldCapture_RoutesToAftermathMenu()
    {
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var siegeEventInterface = new Mock<ISiegeEventInterface>();

        SiegeCaptureMenuHoldPatch.HoldFor(settlement);
        try
        {
            Assert.False(RunPrefix(siegeEventInterface.Object));
        }
        finally
        {
            SiegeCaptureMenuHoldPatch.Release(settlement);
        }

        siegeEventInterface.Verify(service => service.RouteCapturedSettlementToAftermathMenu(settlement), Times.Once);
    }

    [Fact]
    public void FinishedEncounter_WithoutHeldCapture_RunsVanilla()
    {
        var siegeEventInterface = new Mock<ISiegeEventInterface>();

        Assert.True(RunPrefix(siegeEventInterface.Object));

        siegeEventInterface.Verify(
            service => service.RouteCapturedSettlementToAftermathMenu(It.IsAny<Settlement>()),
            Times.Never);
    }

    private static bool RunPrefix(ISiegeEventInterface siegeEventInterface)
    {
        GameBootStrap.Initialize();
        Assert.Null(PlayerEncounter.Current);

        var builder = new ContainerBuilder();
        builder.RegisterInstance(siegeEventInterface);
        using var container = builder.Build();
        ContainerProvider.TryGetContainer(out var previousContainer);
        try
        {
            ContainerProvider.SetContainer(container);
            return EncounterCaptureAftermathInitPatch.Prefix();
        }
        finally
        {
            if (previousContainer != null) ContainerProvider.SetContainer(previousContainer);
            else ContainerProvider.Clear();
        }
    }
}
