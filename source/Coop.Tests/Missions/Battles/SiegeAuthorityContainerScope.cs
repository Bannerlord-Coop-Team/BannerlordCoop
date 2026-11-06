using Autofac;
using GameInterface;
using GameInterface.Services.MapEvents;
using System;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// Points <see cref="ContainerProvider"/> at a container holding one siege authority registry while holding the
/// provider's lock, so siege patches invoked by a test resolve it; the provider is cleared on dispose.
/// </summary>
internal sealed class SiegeAuthorityContainerScope : IDisposable
{
    private readonly IContainer container;
    private readonly IDisposable containerUse;

    /// <summary>Builds the container around the given registry and makes it the provider's container.</summary>
    public SiegeAuthorityContainerScope(ISiegeMachineAuthorityRegistry authority)
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(authority).As<ISiegeMachineAuthorityRegistry>();
        container = builder.Build();

        containerUse = ContainerProvider.UseContainerThreadSafe(container);
    }

    /// <summary>Clears the provider before releasing its lock, so no writer can land between the clear and the release.</summary>
    public void Dispose()
    {
        ContainerProvider.Clear();
        containerUse.Dispose();
        container.Dispose();
    }
}
