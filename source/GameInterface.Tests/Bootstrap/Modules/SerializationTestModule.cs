using Autofac;
using GameInterface.Serialization;
using GameInterface.Services;
using GameInterface.Services.Heroes;
using GameInterface.Services.MobileParties;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Stubs;

namespace GameInterface.Tests.Bootstrap.Modules
{
    internal class SerializationTestModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            builder.RegisterType<ObjectManagerAdapterStub>().As<IObjectManager>().InstancePerLifetimeScope();
            builder.RegisterType<BinaryPackageFactory>().As<IBinaryPackageFactory>();
        }
    }
}
