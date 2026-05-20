using Autofac;
using Coop.Core.Server;
using GameInterface.AutoSync;
using GameInterface.Registry;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.Modules.Interfaces;
using Moq;
using Xunit.Abstractions;

namespace Coop.Tests;

internal class ServerTestComponent : TestComponentBase
{
    public ServerTestComponent(ITestOutputHelper output) : base(output)
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<ServerModule>();
        builder.RegisterType<ControlledEntityRegistry>().As<IControlledEntityRegistry>().InstancePerLifetimeScope();
        builder.RegisterInstance(new Mock<IMobilePartyInterface>().Object).As<IMobilePartyInterface>().SingleInstance();
        builder.RegisterInstance(new Mock<IAutoSyncPatchCollector>().Object).As<IAutoSyncPatchCollector>();
        builder.RegisterModule<RegistryModule>();
        Container = BuildContainer(builder);
    }
}
