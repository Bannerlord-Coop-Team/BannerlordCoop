using Autofac;
using Common.Messaging;
using Coop.Core.Configuration;
using Coop.Tests.Stubs;
using LiteNetLib;

namespace Coop.Tests.Autofac
{
    internal abstract class TestModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<MessageBroker>().As<IMessageBroker>().SingleInstance();
            builder.RegisterType<NetworkConfiguration>().As<INetworkConfiguration>().OwnedByLifetimeScope();
            base.Load(builder);
        }
    }
}
