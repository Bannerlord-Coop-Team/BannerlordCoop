using Autofac;
using Coop.Core.Client;
using Coop.Core.Common.Session;
using GameInterface.Registry;
using Xunit.Abstractions;

namespace Coop.Tests;

internal class ClientTestComponent : TestComponentBase
{
    public ClientTestComponent(ITestOutputHelper output, JoinIntent intent = JoinIntent.PlayerDirect)
        : base(output)
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<ClientModule>();
        builder.RegisterModule<RegistryModule>();

        // Overrides ClientModule's registration, which is pinned to one intent.
        builder.RegisterInstance(JoinAttemptPresentation.For(intent))
            .AsSelf()
            .SingleInstance();

        Container = BuildContainer(builder);
    }
}
