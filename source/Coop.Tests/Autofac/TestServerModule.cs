using Autofac;

namespace Coop.Tests.Autofac
{
    internal class TestServerModule : TestModule
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
        }
    }
}
