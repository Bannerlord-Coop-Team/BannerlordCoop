using Autofac;
using Common.Messaging;
using GameInterface.Serialization;
using GameInterface.Services;
using GameInterface.Services.Heroes;
using GameInterface.Services.Registry;

namespace GameInterface
{
    public class GameInterfaceModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            builder.RegisterType<GameInterface>().As<IGameInterface>().SingleInstance().AutoActivate();
            builder.RegisterType<BinaryPackageFactory>().As<IBinaryPackageFactory>().InstancePerLifetimeScope();
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
