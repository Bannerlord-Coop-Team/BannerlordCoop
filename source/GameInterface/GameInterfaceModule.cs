using Autofac;
using Autofac.Core;
using Autofac.Core.Registration;
using Autofac.Core.Resolving.Pipeline;
using Common.Logging;
using Common.PacketHandlers;
using GameInterface.AutoSync;
using GameInterface.Registry;
using GameInterface.Serialization;
using GameInterface.Services;
using GameInterface.Services.Entity;
using GameInterface.Services.GameDebug.Metrics;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.LiveTesting;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Logging;
using GameInterface.Services.Time;
using GameInterface.Surrogates;
using HarmonyLib;
using Serilog;
using System.Linq;

namespace GameInterface;

public class GameInterfaceModule : Module
{
    // TODO move to config
    public const string HarmonyId = "Bannerlord.Coop";

    private static readonly Harmony harmony = new Harmony(HarmonyId);

    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterInstance(harmony).As<Harmony>().SingleInstance();

        builder.RegisterType<SurrogateCollection>().As<ISurrogateCollection>().InstancePerLifetimeScope().AutoActivate();

        builder.RegisterType<GameInterface>().As<IGameInterface>().InstancePerLifetimeScope().AutoActivate();
        builder.RegisterType<BinaryPackageFactory>().As<IBinaryPackageFactory>().InstancePerLifetimeScope();
        builder.RegisterType<ControllerIdProvider>().As<IControllerIdProvider>().InstancePerLifetimeScope();
        builder.RegisterType<TimeControlModeConverter>().As<ITimeControlModeConverter>().InstancePerLifetimeScope();
        builder.RegisterType<PlayerManager>().As<IPlayerManager>().InstancePerLifetimeScope();
        builder.RegisterType<MobilePartyBehaviorSnapshot>().As<IMobilePartyBehaviorSnapshot>().InstancePerDependency();
        builder.RegisterType<BattleHostRegistry>().As<IBattleHostRegistry>().InstancePerLifetimeScope();
        builder.RegisterType<MapEventContributionBarrier>().As<IMapEventContributionBarrier>().InstancePerDependency();
        builder.RegisterType<PrisonerSaleValidator>().As<IPrisonerSaleValidator>().InstancePerDependency();
        builder.RegisterType<MapEventLogger>().As<IMapEventLogger>().InstancePerLifetimeScope();
        builder.RegisterType<TroopRosterLogger>().As<ITroopRosterLogger>().InstancePerLifetimeScope();
        builder.RegisterType<PartySyncPerformanceClock>().As<IPartySyncPerformanceClock>().InstancePerLifetimeScope();
        builder.RegisterType<PartySyncPerformanceFileWriter>().As<IPartySyncPerformanceFileWriter>().InstancePerLifetimeScope();
        builder.RegisterType<PartySyncPerformancePartyProvider>().As<IPartySyncPerformancePartyProvider>().InstancePerLifetimeScope();
        builder.RegisterType<LiveTestCommandDispatcher>().As<ILiveTestCommandDispatcher>().InstancePerDependency();
        builder.RegisterType<KingdomCreationSettlementTracker>().AsSelf().As<IKingdomCreationSettlementTracker>().InstancePerLifetimeScope();
        builder.RegisterType<KingdomDecisionOutcomeResolver>().AsSelf().As<IKingdomDecisionOutcomeResolver>().InstancePerLifetimeScope();
        builder.RegisterType<KingdomDecisionVoteManager>().AsSelf().As<IKingdomDecisionVoteManager>().InstancePerLifetimeScope();
        builder.RegisterType<KingdomMembershipState>().AsSelf().As<IKingdomMembershipState>().InstancePerLifetimeScope();
        builder.RegisterType<MainPartyBattleRewardsCache>().As<IMainPartyBattleRewardsCache>().InstancePerLifetimeScope();
        builder.RegisterType<PacketManager>().As<IPacketManager>().InstancePerLifetimeScope();

        builder.RegisterModule<ServiceModule>();
        builder.RegisterModule<ObjectManagerModule>();
        builder.RegisterModule<RegistryModule>();
        builder.RegisterModule<AutoSyncModule>();


        base.Load(builder);
    }

    // Log injector
    protected override void AttachToComponentRegistration(IComponentRegistryBuilder componentRegistry, IComponentRegistration registration)
    {
        registration.PipelineBuilding += (sender, pipeline) =>
        {
            pipeline.Use(PipelinePhase.Activation, MiddlewareInsertionMode.StartOfPhase, (c, next) =>
            {
                var forType = c.Registration.Activator.LimitType;

                var logParameter = new ResolvedParameter(
                    (p, c) => p.ParameterType == typeof(ILogger),
                    (p, c) => AccessTools.Method(typeof(LogManager), nameof(LogManager.GetLogger)).MakeGenericMethod(forType).Invoke(null, null) as ILogger);

                c.GetType().Property(nameof(c.Parameters)).SetValue(c, c.Parameters.Union(new[] { logParameter }));

                next(c);
            });
        };
    }
}
