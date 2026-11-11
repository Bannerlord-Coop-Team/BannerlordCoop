using GameInterface.Registry;
using GameInterface.Services.Clans;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobileParties;
using Moq;
using Serilog;
using System;
using Xunit;

namespace GameInterface.Tests.Registry;

public class LoadRepairRunnerTests
{
    [Fact]
    public void RunLoadRepairs_AStepThrows_RunsTheRemainingSteps()
    {
        var registryManager = new Mock<IRegistryManager>();
        var reconciler = new Mock<ILoadRepairRegistryReconciler>();
        var cleaner = new Mock<IMapEventLoadCleaner>();
        var clanResolver = new Mock<ILeaderlessClanResolver>();
        var partyResetter = new Mock<IEmptyLordPartyResetter>();
        reconciler.Setup(r => r.Capture()).Returns(LoadRepairSnapshot.Empty);
        cleaner.Setup(c => c.CleanUpLoadedMapEvents()).Throws(new InvalidOperationException("cleaner failed"));

        new LoadRepairRunner(
            new LoggerConfiguration().CreateLogger(),
            registryManager.Object,
            reconciler.Object,
            cleaner.Object,
            clanResolver.Object,
            partyResetter.Object).RunLoadRepairs();

        clanResolver.Verify(r => r.RepairLoadedClans(), Times.Once);
        partyResetter.Verify(r => r.ResetEmptyLordParties(), Times.Once);
        reconciler.Verify(r => r.UnregisterRemoved(LoadRepairSnapshot.Empty), Times.Once);
        registryManager.Verify(r => r.RegisterNewGameObjects(), Times.Once);
    }
}
