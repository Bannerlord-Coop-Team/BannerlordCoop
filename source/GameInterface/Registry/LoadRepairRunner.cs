using GameInterface.Services.Clans;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobileParties;
using Serilog;
using System;

namespace GameInterface.Registry;

public interface ILoadRepairRunner
{
    // Runs the repairs a loaded save needs, each caught on its own so one failing repair cannot stop the server from starting.
    void RunLoadRepairs();
}

// Runs the load repairs once the server is running, so the lifetime patches register and announce what they change.
internal sealed class LoadRepairRunner : ILoadRepairRunner
{
    private readonly ILogger logger;
    private readonly IMapEventLoadCleaner mapEventLoadCleaner;
    private readonly ILeaderlessClanResolver leaderlessClanResolver;
    private readonly IEmptyLordPartyResetter emptyLordPartyResetter;

    public LoadRepairRunner(
        ILogger logger,
        IMapEventLoadCleaner mapEventLoadCleaner,
        ILeaderlessClanResolver leaderlessClanResolver,
        IEmptyLordPartyResetter emptyLordPartyResetter)
    {
        this.logger = logger;
        this.mapEventLoadCleaner = mapEventLoadCleaner;
        this.leaderlessClanResolver = leaderlessClanResolver;
        this.emptyLordPartyResetter = emptyLordPartyResetter;
    }

    public void RunLoadRepairs()
    {
        RunStep("clean up loaded map events", mapEventLoadCleaner.CleanUpLoadedMapEvents);
        RunStep("repair leaderless clans", () => leaderlessClanResolver.RepairLoadedClans());
        RunStep("reset empty lord parties", () => emptyLordPartyResetter.ResetEmptyLordParties());
    }

    // Runs one repair step, logging a throw so the remaining steps still run.
    private void RunStep(string step, Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            logger.Error(e, "Load repair step '{Step}' failed; the server starts without it", step);
        }
    }
}
