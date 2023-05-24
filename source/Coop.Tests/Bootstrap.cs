using Autofac;
using Common.Network;
using Coop.Core.Server;
using Coop.Tests.Autofac;

namespace Coop.Tests
{
    internal class Bootstrap
    {
        internal static IContainer InitializeAsClient()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterType<CoopServer>().As<INetwork>();
            builder.RegisterModule<TestClientModule>();
            return builder.Build();
        }

        internal static IContainer InitializeAsServer()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<TestServerModule>();
            return builder.Build();
        }
    }
}