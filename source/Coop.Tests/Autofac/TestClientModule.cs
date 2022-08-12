using Autofac;
using Coop.Communication.MessageBroker;
using Coop.Tests.Stubs;

namespace Coop.Tests.Autofac
{
    internal class TestClientModule : TestModule
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
        }
    }
}
