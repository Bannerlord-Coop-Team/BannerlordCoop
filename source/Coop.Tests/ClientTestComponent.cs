using Autofac;
using Coop.Core.Client;
using GameInterface.AutoSync;
using GameInterface.Registry;
using GameInterface.Services.Entity;
using Moq;
using Xunit.Abstractions;

namespace Coop.Tests;

internal class ClientTestComponent : TestComponentBase
{
    public ClientTestComponent(ITestOutputHelper output) : base(output)
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<ClientModule>();
        builder.RegisterType<ControlledEntityRegistry>().As<IControlledEntityRegistry>().InstancePerLifetimeScope();
        builder.RegisterInstance(new Mock<IAutoSyncPatchCollector>().Object).As<IAutoSyncPatchCollector>();
        builder.RegisterModule<RegistryModule>();

        Container = BuildContainer(builder);
    }
}
