using Autofac;
using GameInterface;
using GameInterface.Services.MapEvents;
using System;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>
/// Installs a container whose coop battle registry has a battle window open, plus the battle call-context trackers,
/// so patches invoked without an environment see a coop battle. Restores the previous container when disposed.
/// </summary>
// Coop.Tests keeps its own copy, because E2E must not load Coop.Tests, whose module initializer starts a game-loop pump.
public sealed class ActiveCoopBattleScope : IDisposable
{
    private readonly IContainer container;
    private readonly ILifetimeScope previousContainer;
    private readonly bool hadPreviousContainer;

    // Builds the registry container, installs it, and opens a battle window for mapEventId (null leaves it closed).
    public ActiveCoopBattleScope(string mapEventId)
    {
        var builder = new ContainerBuilder();
        builder.RegisterType<CoopBattleMissionRegistry>().As<ICoopBattleMissionRegistry>().SingleInstance();
        builder.RegisterType<PuppetSpawnTracker>().As<IPuppetSpawnTracker>().SingleInstance();
        builder.RegisterType<RoutedBlowTracker>().As<IRoutedBlowTracker>().SingleInstance();
        builder.RegisterType<ReplicatedDeathTracker>().As<IReplicatedDeathTracker>().SingleInstance();
        container = builder.Build();
        Registry = container.Resolve<ICoopBattleMissionRegistry>();

        hadPreviousContainer = ContainerProvider.TryGetContainer(out previousContainer);
        ContainerProvider.SetContainer(container);
        Registry.BeginBattle(mapEventId);
    }

    /// <summary>The installed registry, for tests that close or inspect the battle window mid-test.</summary>
    public ICoopBattleMissionRegistry Registry { get; }

    // Closes the battle window and puts the previous container back.
    public void Dispose()
    {
        Registry.EndBattle();
        if (hadPreviousContainer) ContainerProvider.SetContainer(previousContainer);
        else ContainerProvider.Clear();
        container.Dispose();
    }
}
