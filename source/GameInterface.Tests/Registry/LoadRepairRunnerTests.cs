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
        var cleaner = new Mock<IMapEventLoadCleaner>();
        var clanResolver = new Mock<ILeaderlessClanResolver>();
        var partyResetter = new Mock<IEmptyLordPartyResetter>();
        cleaner.Setup(c => c.CleanUpLoadedMapEvents()).Throws(new InvalidOperationException("cleaner failed"));

        new LoadRepairRunner(
            new LoggerConfiguration().CreateLogger(),
            cleaner.Object,
            clanResolver.Object,
            partyResetter.Object).RunLoadRepairs();

        clanResolver.Verify(r => r.RepairLoadedClans(), Times.Once);
        partyResetter.Verify(r => r.ResetEmptyLordParties(), Times.Once);
    }
}
