using Autofac;
using GameInterface;
using GameInterface.Services.MapEvents;
using System;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// Points <see cref="ContainerProvider"/> at a container holding one siege authority registry, so siege
/// patches invoked by a test resolve it; the previous container is restored on dispose.
/// </summary>
internal sealed class SiegeAuthorityContainerScope : IDisposable
{
    private readonly IContainer container;
    private readonly IDisposable containerUse;
    private readonly bool hadPreviousContainer;
    private readonly ILifetimeScope previousContainer;

    /// <summary>Builds the container around the given registry and makes it the provider's container.</summary>
    public SiegeAuthorityContainerScope(ISiegeMachineAuthorityRegistry authority)
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(authority).As<ISiegeMachineAuthorityRegistry>();
        container = builder.Build();

        hadPreviousContainer = ContainerProvider.TryGetContainer(out previousContainer);
        containerUse = ContainerProvider.UseContainerThreadSafe(container);
    }

    /// <summary>Restores the container the provider held before this scope.</summary>
    public void Dispose()
    {
        containerUse.Dispose();
        if (hadPreviousContainer)
            ContainerProvider.SetContainer(previousContainer);
        else
            ContainerProvider.Clear();

        container.Dispose();
    }
}
