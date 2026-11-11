#if COOP_NAVAL
using Autofac;
using GameInterface;
using Missions.Naval;
using System;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// A client container built from <see cref="NavalMissionModule"/>, active in ContainerProvider until disposed, so the naval
/// patches under test resolve this client's naval mission state.
/// </summary>
internal sealed class NavalClientContainer : IDisposable
{
    private readonly IContainer container;
    private readonly IDisposable activeUse;

    // Builds the client container and makes it the one patches resolve from.
    public NavalClientContainer()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<NavalMissionModule>();
        container = builder.Build();
        activeUse = ContainerProvider.UseContainerThreadSafe(container);
    }

    /// <summary>This client's naval mission tracker.</summary>
    public INavalMissionTracker MissionTracker => container.Resolve<INavalMissionTracker>();

    // Resolves a service of this client.
    public T Resolve<T>() => container.Resolve<T>();

    // Drops the container from ContainerProvider and disposes it.
    public void Dispose()
    {
        ContainerProvider.Clear();
        activeUse.Dispose();
        container.Dispose();
    }
}
#endif
