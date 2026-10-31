using GameInterface.Services.Clans;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobileParties;
using Serilog;
using System;

namespace GameInterface.Registry;

public interface ILoadRepairRunner
{
    /// <summary>
    /// Runs the repairs a loaded save needs before the server becomes joinable, then brings the server
    /// registry in line with the parties and armies they created or destroyed. Each step is caught on
    /// its own, so one failing repair cannot stop the server from starting.
    /// </summary>
    void RunLoadRepairs();
}

/// <summary>
/// The repairs run after the first registration and before ServerRunningState, while the sync policy
/// still allows originals, so lifetime patches publish no InstanceCreated or InstanceDestroyed for what
/// they do. A clan repair can create a lord party and the empty party reset destroys parties and
/// disbands armies; without this pass the party stays unregistered (the join baseline then fails for
/// it) or the destroyed object keeps its id.
/// </summary>
internal sealed class LoadRepairRunner : ILoadRepairRunner
{
    private readonly ILogger logger;
    private readonly IRegistryManager registryManager;
    private readonly ILoadRepairRegistryReconciler registryReconciler;
    private readonly IMapEventLoadCleaner mapEventLoadCleaner;
    private readonly ILeaderlessClanResolver leaderlessClanResolver;
    private readonly IEmptyLordPartyResetter emptyLordPartyResetter;

    public LoadRepairRunner(
        ILogger logger,
        IRegistryManager registryManager,
        ILoadRepairRegistryReconciler registryReconciler,
        IMapEventLoadCleaner mapEventLoadCleaner,
        ILeaderlessClanResolver leaderlessClanResolver,
        IEmptyLordPartyResetter emptyLordPartyResetter)
    {
        this.logger = logger;
        this.registryManager = registryManager;
        this.registryReconciler = registryReconciler;
        this.mapEventLoadCleaner = mapEventLoadCleaner;
        this.leaderlessClanResolver = leaderlessClanResolver;
        this.emptyLordPartyResetter = emptyLordPartyResetter;
    }

    public void RunLoadRepairs()
    {
        LoadRepairSnapshot before = null;
        RunStep("capture campaign parties and armies", () => before = registryReconciler.Capture());

        RunStep("clean up loaded map events", mapEventLoadCleaner.CleanUpLoadedMapEvents);
        RunStep("repair leaderless clans", () => leaderlessClanResolver.RepairLoadedClans());
        RunStep("reset empty lord parties", () => emptyLordPartyResetter.ResetEmptyLordParties());

        // Unregister first, so a created object can never collide with the stale id of a destroyed one.
        if (before != null)
            RunStep("unregister removed parties and armies", () => registryReconciler.UnregisterRemoved(before));
        RunStep("register created objects", registryManager.RegisterNewGameObjects);
    }

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
