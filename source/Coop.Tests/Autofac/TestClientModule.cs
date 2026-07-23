using Autofac;
using Common.Network;
using Coop.Core.Client;

namespace Coop.Tests.Autofac
{
    internal class TestClientModule : TestModule
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<CoopClient>().As<INetwork>();
            base.Load(builder);
        }
    }
}
