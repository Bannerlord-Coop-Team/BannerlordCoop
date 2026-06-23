using Autofac;
using Common.Messaging;
using Common.Network;
using Common.Tests.Utils;
using Coop.Core.Common.Configuration;

namespace Coop.Tests.Autofac
{
    internal abstract class TestModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<TestMessageBroker>().AsSelf().As<IMessageBroker>().InstancePerLifetimeScope();
            builder.RegisterType<NetworkConfig>().As<INetworkConfig>().OwnedByLifetimeScope();
            base.Load(builder);
        }
    }
}
