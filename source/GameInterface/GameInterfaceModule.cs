using Autofac;
using GameInterface.Serialization;
using GameInterface.Services;
using GameInterface.Services.Clans;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Registry;
using GameInterface.Services.Settlements;
using GameInterface.Services.Time;

namespace GameInterface;

public class GameInterfaceModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);
        builder.RegisterType<GameInterface>().As<IGameInterface>().InstancePerLifetimeScope().AutoActivate();
        builder.RegisterType<ObjectManager>().As<IObjectManager>().InstancePerLifetimeScope();
        builder.RegisterType<BinaryPackageFactory>().As<IBinaryPackageFactory>().InstancePerLifetimeScope();
        builder.RegisterType<ControllerIdProvider>().As<IControllerIdProvider>().InstancePerLifetimeScope();
        builder.RegisterType<ControlledEntityRegistry>().As<IControlledEntityRegistry>().InstancePerLifetimeScope();
        builder.RegisterType<TimeControlModeConverter>().As<ITimeControlModeConverter>().InstancePerLifetimeScope();
        builder.RegisterType<PlayerRegistry>().As<IPlayerRegistry>().InstancePerLifetimeScope();
        builder.RegisterModule<ServiceModule>();

        builder.RegisterType<MobilePartyRegistry>()
               .As<IMobilePartyRegistry>()
               .InstancePerLifetimeScope();

        builder.RegisterType<HeroRegistry>()
               .As<IHeroRegistry>()
               .InstancePerLifetimeScope();

        builder.RegisterType<ClanRegistry>()
               .As<IClanRegistry>()
               .InstancePerLifetimeScope();

        builder.RegisterType<ControlledEntityRegistry>()
               .As<IControlledEntityRegistry>()
               .InstancePerLifetimeScope();
    }
}
