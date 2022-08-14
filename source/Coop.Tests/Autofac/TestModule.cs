using Autofac;
using Common.Messages;
using Coop.Tests.Stubs;

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
