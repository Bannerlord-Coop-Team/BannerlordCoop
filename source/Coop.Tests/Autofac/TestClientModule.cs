using Autofac;
using Common.Messages;

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
