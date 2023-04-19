using Autofac;
using Common.Messaging;
using GameInterface.Serialization;
using GameInterface.Services;
using GameInterface.Services.Heroes;
using GameInterface.Services.MobileParties;
using GameInterface.Services.ObjectManager;

namespace GameInterface
{
    public class GameInterfaceModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            builder.RegisterType<GameInterface>().As<IGameInterface>().SingleInstance().AutoActivate();
            builder.RegisterType<MBObjectManagerAdapter>().As<IObjectManager>().InstancePerLifetimeScope();
            builder.RegisterType<BinaryPackageFactory>().As<IBinaryPackageFactory>();
            builder.RegisterModule<ServiceModule>();

            builder.RegisterType<MobilePartyRegistry>()
                   .As<IMobilePartyRegistry>()
                   .InstancePerLifetimeScope();

            builder.RegisterType<HeroRegistry>()
                   .As<IHeroRegistry>()
                   .InstancePerLifetimeScope();

            builder.RegisterType<ControlledHeroRegistry>()
                   .As<IControlledHeroRegistry>()
                   .InstancePerLifetimeScope();
        }
    }
}
