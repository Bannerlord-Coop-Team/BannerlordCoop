using Autofac;
using Coop.Core.Configuration;
using Coop.Core.Server;
using LiteNetLib;

namespace Coop.Tests.Autofac
{
    internal class TestServerModule : TestModule
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<CoopServer>().As<ICoopServer>().SingleInstance().PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);
            base.Load(builder);
        }
    }
}
