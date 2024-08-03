using Autofac;
using GameInterface.AutoSync;
using GameInterface.Serialization;
using GameInterface.Services;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Time;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface;

public class GameInterfaceModule : Module
{
    // TODO move to config
    public const string HarmonyId = "TaleWorlds.MountAndBlade.Bannerlord.Coop";

    protected override void Load(ContainerBuilder builder)
    {
        
        builder.RegisterInstance(new Harmony(HarmonyId)).As<Harmony>().SingleInstance();

        builder.RegisterType<GameInterface>().As<IGameInterface>().InstancePerLifetimeScope().AutoActivate();
        builder.RegisterType<BinaryPackageFactory>().As<IBinaryPackageFactory>().InstancePerLifetimeScope();
        builder.RegisterType<ControllerIdProvider>().As<IControllerIdProvider>().InstancePerLifetimeScope();
        builder.RegisterType<ControlledEntityRegistry>().As<IControlledEntityRegistry>().InstancePerLifetimeScope();
        builder.RegisterType<TimeControlModeConverter>().As<ITimeControlModeConverter>().InstancePerLifetimeScope();
        builder.RegisterType<PlayerRegistry>().As<IPlayerRegistry>().InstancePerLifetimeScope();

        // Autosync
        builder.RegisterType<AutoSyncPatcher>().As<IAutoSyncPatcher>().InstancePerLifetimeScope();
        builder.RegisterType<AutoSyncTypeMapper>().As<IAutoSyncTypeMapper>().InstancePerLifetimeScope();
        

        builder.RegisterModule<ServiceModule>();
        builder.RegisterModule<ObjectManagerModule>();

        // Generic class registration, getting any template type in any constructor will resolve this class
        builder.RegisterGeneric(typeof(AutoSyncBuilder<>)).As(typeof(IAutoSyncBuilder<>)).InstancePerLifetimeScope();


        base.Load(builder);
    }
}
