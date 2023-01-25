using Autofac;
using Common.Messaging;
using GameInterface.Serialization;
using GameInterface.Services;

namespace GameInterface
{
    public class GameInterfaceModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            builder.RegisterType<GameInterface>().As<IGameInterface>().SingleInstance();
            builder.RegisterType<BinaryPackageFactory>().As<IBinaryPackageFactory>().SingleInstance();
            builder.RegisterModule<ServiceModule>();
        }
    }
}
