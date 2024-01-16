using Autofac;
using Common.Messaging;
using Common.Network;
using Common.Tests.Utils;
using Coop.Core;
using Coop.Core.Common.Configuration;
using Coop.Tests.Stubs;
using LiteNetLib;

namespace Coop.Tests.Autofac
{
    internal abstract class TestModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<TestMessageBroker>().AsSelf().As<IMessageBroker>().InstancePerLifetimeScope();
            builder.RegisterType<ContainerProvider>().As<IContainerProvider>().InstancePerLifetimeScope();
            builder.RegisterType<NetworkConfiguration>().As<INetworkConfiguration>().OwnedByLifetimeScope();
            base.Load(builder);
        }
    }
}
