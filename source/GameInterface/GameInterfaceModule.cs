using Autofac;
using Autofac.Core;
using Autofac.Core.Registration;
using Autofac.Core.Resolving.Pipeline;
using Common.Logging;
using Common.PacketHandlers;
using GameInterface.AutoSync;
using GameInterface.Configuration;
using GameInterface.Registry;
using GameInterface.Serialization;
using GameInterface.Services;
using GameInterface.Services.Armies;
using GameInterface.Services.Bandits;
using GameInterface.Services.Barters;
using GameInterface.Services.Chat;
using GameInterface.Services.Entity;
using GameInterface.Services.GameDebug.Metrics;
using GameInterface.Services.Heroes;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Services.LiveTesting;
using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Hosting;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MobileParties;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.Modules;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party;
using GameInterface.Services.Players;
using GameInterface.Services.Stances;
using GameInterface.Services.Time;
using GameInterface.Services.TroopRosters;
using GameInterface.Services.TroopRosters.Logging;
using GameInterface.Services.Workshops;
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
        // mod-config.json: one lazy read per session container (see IModConfig).
        builder.RegisterType<ModConfig>().As<IModConfig>().InstancePerLifetimeScope();
        builder.RegisterType<BinaryPackageFactory>().As<IBinaryPackageFactory>().InstancePerLifetimeScope();
        builder.RegisterType<ControllerIdProvider>().As<IControllerIdProvider>().InstancePerLifetimeScope();
        builder.RegisterType<TimeControlModeConverter>().As<ITimeControlModeConverter>().InstancePerLifetimeScope();
        builder.RegisterType<PlayerManager>().As<IPlayerManager>().InstancePerLifetimeScope();
        builder.RegisterType<ChatPlayerName>().As<IChatPlayerNameResolver>().InstancePerDependency();
        builder.RegisterType<PlayerPartyRestorer>().As<IPlayerPartyRestorer>().InstancePerDependency();
        builder.RegisterType<PlayerCreationRollback>().As<IPlayerCreationRollback>().InstancePerDependency();
        builder.RegisterType<MobilePartyBehaviorSnapshot>().As<IMobilePartyBehaviorSnapshot>().InstancePerDependency();
        builder.RegisterType<BarterClientPresentation>().As<IBarterClientPresentation>().InstancePerDependency();
        builder.RegisterType<SafePassagePartyResolver>().AsSelf().As<ISafePassagePartyResolver>().InstancePerDependency();
        builder.RegisterType<PeacePursuitCleaner>().As<IPeacePursuitCleaner>().InstancePerDependency();
        builder.RegisterType<PartyVisibilitySweep>().As<IPartyVisibilitySweep>().InstancePerDependency();
        builder.RegisterType<ConversationRestartContextTracker>().As<IConversationRestartContextTracker>().InstancePerLifetimeScope();
        builder.RegisterType<IssueConversationTracker>().As<IIssueConversationTracker>().InstancePerLifetimeScope();
        builder.RegisterType<IssueOwnershipRegistry>().As<IIssueOwnershipRegistry>().InstancePerLifetimeScope();
        builder.RegisterType<IssueGenerationRegistry>().As<IIssueGenerationRegistry>().InstancePerLifetimeScope();
        builder.RegisterType<AwaitingAlternativeSolutionTroopsRegistry>().As<IAwaitingAlternativeSolutionTroopsRegistry>().InstancePerLifetimeScope();
        builder.RegisterType<BattleHostRegistry>().As<IBattleHostRegistry>().InstancePerLifetimeScope();
        builder.RegisterType<LocationHostRegistry>().As<ILocationHostRegistry>().InstancePerLifetimeScope();
        builder.RegisterType<BattleAgentBudget>().As<IBattleAgentBudget>().InstancePerDependency();
        builder.RegisterType<SiegeMapEventLeaderReconciler>().As<ISiegeMapEventLeaderReconciler>().InstancePerDependency();
        builder.RegisterType<MapEventContributionBarrier>().As<IMapEventContributionBarrier>().InstancePerDependency();
        builder.RegisterType<ArmyDisbander>().As<IArmyDisbander>().InstancePerDependency();
        builder.RegisterType<MapEventLoadCleaner>().As<IMapEventLoadCleaner>().InstancePerDependency();
        builder.RegisterType<EncounterMenuConditionRefresher>().As<IEncounterMenuConditionRefresher>().InstancePerDependency();
        builder.RegisterType<PartyScreenRosterRefresher>().As<IPartyScreenRosterRefresher>().InstancePerDependency();
        builder.RegisterType<PlayerTroopXpRelevance>().As<IPlayerTroopXpRelevance>().InstancePerDependency();
        builder.RegisterType<PrisonerSaleValidator>().As<IPrisonerSaleValidator>().InstancePerDependency();
        builder.RegisterType<PlayerRansomReleaseSettlementProvider>().As<IPlayerRansomReleaseSettlementProvider>().InstancePerDependency();
        builder.RegisterType<SessionHeroMeetingDataInterface>().As<ISessionHeroMeetingDataInterface>().InstancePerDependency();
        builder.RegisterType<PrisonerSaleProcessor>().As<IPrisonerSaleProcessor>().InstancePerDependency();
        builder.RegisterType<PartyScreenRosterBaselineProvider>().As<IPartyScreenRosterBaselineProvider>().InstancePerDependency();
        builder.RegisterType<BanditPartyHomeSettlementRepairer>().As<IBanditPartyHomeSettlementRepairer>().InstancePerDependency();
        builder.RegisterType<DeadHeroCaptivityRepairer>().As<IDeadHeroCaptivityRepairer>().InstancePerDependency();
        builder.RegisterType<WorkshopRepairer>().As<IWorkshopRepairer>().InstancePerDependency();
        builder.RegisterType<ModuleRescanCompletionRunner>().As<IModuleRescanCompletionRunner>().InstancePerDependency();
        builder.RegisterType<MapEventLogger>().As<IMapEventLogger>().InstancePerLifetimeScope();
        builder.RegisterType<TroopRosterLogger>().As<ITroopRosterLogger>().InstancePerLifetimeScope();
        builder.RegisterType<PartySyncPerformanceClock>().As<IPartySyncPerformanceClock>().InstancePerLifetimeScope();
        builder.RegisterType<PartySyncPerformanceFileWriter>().As<IPartySyncPerformanceFileWriter>().InstancePerLifetimeScope();
        builder.RegisterType<PartySyncPerformancePartyProvider>().As<IPartySyncPerformancePartyProvider>().InstancePerLifetimeScope();
        builder.RegisterType<LiveTestCommandDispatcher>().As<ILiveTestCommandDispatcher>().InstancePerDependency();
        builder.RegisterType<CoopModulePathResolver>().As<ICoopModulePathResolver>().InstancePerDependency();
        builder.RegisterType<FixedTownNpcService>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<KingdomCreationSettlementTracker>().AsSelf().As<IKingdomCreationSettlementTracker>().InstancePerLifetimeScope();
        builder.RegisterType<KingdomCreator>().AsSelf().As<IKingdomCreator>().InstancePerLifetimeScope();
        builder.RegisterType<KingdomDecisionOutcomeResolver>().AsSelf().As<IKingdomDecisionOutcomeResolver>().InstancePerLifetimeScope();
        builder.RegisterType<KingdomDecisionVoteManager>().AsSelf().As<IKingdomDecisionVoteManager>().InstancePerLifetimeScope();
        builder.RegisterType<KingdomMembershipState>().AsSelf().As<IKingdomMembershipState>().InstancePerLifetimeScope();
        builder.RegisterType<ClientClanStrengthRefresher>().As<IClientClanStrengthRefresher>().InstancePerDependency();
        builder.RegisterType<MainPartyBattleRewardsCache>().As<IMainPartyBattleRewardsCache>().InstancePerLifetimeScope();
        builder.RegisterType<PacketManager>().As<IPacketManager>().InstancePerLifetimeScope();
        builder.RegisterType<MapEventInitializationBarrierBinding>().InstancePerLifetimeScope().AutoActivate();

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
