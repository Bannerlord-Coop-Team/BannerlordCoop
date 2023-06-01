using Autofac;
using GameInterface.Serialization;
using GameInterface.Services.ObjectManager;
using GameInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.IntegrationTests.Environment;

internal class TestEnvironmentModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);
        builder.RegisterType<GameInterface>().As<IGameInterface>().SingleInstance().AutoActivate();
        builder.RegisterType<MBObjectManagerAdapter>().As<IObjectManager>().InstancePerLifetimeScope();
        builder.RegisterType<BinaryPackageFactory>().As<IBinaryPackageFactory>().InstancePerLifetimeScope();
        builder.RegisterModule<ServiceModule>();

        builder.RegisterType<MobilePartyRegistry>()
               .As<IMobilePartyRegistry>()
               .InstancePerLifetimeScope();

        builder.RegisterType<HeroRegistry>()
               .As<IHeroRegistry>()
               .InstancePerLifetimeScope();

        builder.RegisterType<ControlledEntityRegistry>()
               .As<IControlledEntityRegistry>()
               .InstancePerLifetimeScope();
    }
}
