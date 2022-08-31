using Autofac;
using Common.Messaging;

namespace Coop.Tests.Autofac
{
    internal abstract class TestModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<MessageBroker>().As<IMessageBroker>().SingleInstance();

            base.Load(builder);
        }
    }
}
