using Autofac;
using Coop.Communication.MessageBroker;
using Coop.Tests.Stubs;

namespace Coop.Tests.Autofac
{
    internal abstract class TestModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<MessageBrokerStub>().As<IMessageBroker>().SingleInstance();

            base.Load(builder);
        }
    }
}
