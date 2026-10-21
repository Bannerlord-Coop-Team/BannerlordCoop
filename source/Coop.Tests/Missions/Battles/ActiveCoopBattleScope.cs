using Autofac;
using GameInterface;
using GameInterface.Services.MapEvents;
using System;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// Installs a container whose coop battle registry has a battle window open, so patches invoked without an
/// environment see a coop battle. Holds the container slot until disposed, then restores the previous one.
/// </summary>
internal sealed class ActiveCoopBattleScope : IDisposable
{
    private readonly IContainer container;
    private readonly IDisposable containerUse;
    private readonly ILifetimeScope previousContainer;
    private readonly bool hadPreviousContainer;

    // Builds the registry container, installs it, and opens a battle window for mapEventId (null leaves it closed).
    public ActiveCoopBattleScope(string mapEventId)
    {
        var builder = new ContainerBuilder();
        builder.RegisterType<CoopBattleMissionRegistry>().As<ICoopBattleMissionRegistry>().SingleInstance();
        container = builder.Build();
        Registry = container.Resolve<ICoopBattleMissionRegistry>();

        hadPreviousContainer = ContainerProvider.TryGetContainer(out previousContainer);
        containerUse = ContainerProvider.UseContainerThreadSafe(container);
        Registry.BeginBattle(mapEventId);
    }

    /// <summary>The installed registry, for tests that close or inspect the battle window mid-test.</summary>
    public ICoopBattleMissionRegistry Registry { get; }

    // Closes the battle window and puts the previous container back.
    public void Dispose()
    {
        Registry.EndBattle();
        containerUse.Dispose();
        if (hadPreviousContainer) ContainerProvider.SetContainer(previousContainer);
        else ContainerProvider.Clear();
        container.Dispose();
    }
}
