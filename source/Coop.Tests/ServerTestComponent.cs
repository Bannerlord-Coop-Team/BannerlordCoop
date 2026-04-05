using Autofac;
using Coop.Core.Server;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Interfaces;
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
        Container = BuildContainer(builder);
    }
}
