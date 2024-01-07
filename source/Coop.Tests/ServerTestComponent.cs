using Autofac;
using Coop.Core.Server;
using Xunit.Abstractions;

namespace Coop.Tests;

internal class ServerTestComponent : TestComponentBase
{
    public ServerTestComponent(ITestOutputHelper output) : base(output)
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<ServerModule>();
        Container = BuildContainer(builder);
    }
}
