using Autofac;
using Coop.Core.Server;
using GameInterface.Registry;
using Xunit.Abstractions;

namespace Coop.Tests;

internal class ServerTestComponent : TestComponentBase
{
    public ServerTestComponent(ITestOutputHelper output) : base(output)
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<ServerModule>();
        builder.RegisterModule<RegistryModule>();

        RegisterMock<ICoopServer>(builder);

        Container = BuildContainer(builder);
    }
}
