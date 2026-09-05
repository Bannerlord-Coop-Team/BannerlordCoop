using Autofac;
using Autofac.Core;
using Autofac.Core.Registration;
using Autofac.Core.Resolving.Pipeline;
using Common.Commands;
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
using GameInterface.Services.BugReporting;
using GameInterface.Services.Chat;
using GameInterface.Services.Entity;
using GameInterface.Services.GameDebug.Metrics;
using GameInterface.Services.Heroes;
using GameInterface.Services.Heroes.Commands;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Services.LiveTesting;
using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Conversations;
using GameInterface.Services.Locations.Hosting;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MobileParties;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobilePartyAIs;
using GameInterface.Services.Modules;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party;
using GameInterface.Services.Players;
using GameInterface.Services.SiegeEvents;
using GameInterface.Services.Stances;
using GameInterface.Services.Time;
using GameInterface.Services.TroopRosters;
using GameInterface.Services.TroopRosters.Logging;
using GameInterface.Services.UI.CoopOptions.Providers;
using GameInterface.Services.UI.CoopOptions.Providers.BugReportTab;
using GameInterface.Services.UI.CoopOptions.Providers.ChatTab;
using GameInterface.Services.UI.CoopOptions.Providers.KillFeedTab;
using GameInterface.Services.UI.CoopOptions.Providers.MapTimeTab;
using GameInterface.Services.UI.CoopOptions.Providers.NetworkTab;
using GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab;
using GameInterface.Services.UI.BugReporting;
using GameInterface.Services.UI.Patches;
using GameInterface.Services.Workshops;
using GameInterface.Surrogates;
using GameInterface.Utils.Commands;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GameInterface;

public class GameInterfaceModule : Module
{
    // TODO move to config
    public const string HarmonyId = "Bannerlord.Coop";

    private static readonly Harmony harmony = new Harmony(HarmonyId);

    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterInstance(harmony).As<Harmony>().SingleInstance();
        builder.RegisterInstance(new CoopLogFile(null))
            .As<ICoopLogFile>()
            .SingleInstance()
            .PreserveExistingDefaults();
        builder.Register(_ => new CancellationTokenSource())
            .InstancePerLifetimeScope()
            .PreserveExistingDefaults();

        builder.RegisterType<SurrogateCollection>().As<ISurrogateCollection>().InstancePerLifetimeScope().AutoActivate();

        builder.RegisterType<CoopCommandArgsFactory>().As<ICoopCommandArgsFactory>().InstancePerDependency();
        builder.RegisterType<RglCommandLineRegistry>().As<IRglCommandLineRegistry>().InstancePerDependency();
        builder.Register(context => new CoopCommandRegistry(
                context.Resolve<IEnumerable<ICoopCommand>>(),
                LogManager.GetLogger<CoopCommandRegistry>(),
                CoopCommandLegacyAliases.Map))
            .As<ICoopCommandRegistry>()
            .InstancePerLifetimeScope();
        builder.RegisterAssemblyTypes(typeof(GameInterfaceModule).Assembly)
            .Where(type => type.IsClass &&
                           !type.IsAbstract &&
                           typeof(ICoopCommand).IsAssignableFrom(type))
            .As(type => type.GetInterfaces()
                .Where(interfaceType => interfaceType != typeof(ICoopCommand)))
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<CoopCommandLineRegistrar>()
            .As<ICoopCommandLineRegistrar>()
            .InstancePerLifetimeScope()
            .AutoActivate();

        builder.RegisterType<GameInterface>().As<IGameInterface>().InstancePerLifetimeScope().AutoActivate();
        // mod-config.json: one lazy read per session container (see IModConfig).
        builder.RegisterType<ModConfig>().As<IModConfig>().InstancePerLifetimeScope();
        builder.RegisterType<BinaryPackageFactory>().As<IBinaryPackageFactory>().InstancePerLifetimeScope();
        builder.RegisterType<ControllerIdProvider>().As<IControllerIdProvider>().InstancePerLifetimeScope();
        builder.RegisterType<TimeControlModeConverter>().As<ITimeControlModeConverter>().InstancePerLifetimeScope();
        builder.RegisterType<PlayerManager>().As<IPlayerManager>().InstancePerLifetimeScope();
        builder.RegisterType<BugReportService>().As<IBugReportService>().InstancePerLifetimeScope().AutoActivate();
        builder.RegisterType<BugReportOverlay>().As<IBugReportOverlay>().InstancePerLifetimeScope();
        builder.RegisterType<CoopLogSnapshotProvider>().As<ICoopLogSnapshotProvider>().InstancePerDependency();
        builder.RegisterType<BugReportServerSaveProvider>().As<IBugReportServerSaveProvider>().InstancePerDependency();
        builder.RegisterType<BugReportArchiveBuilder>().As<IBugReportArchiveBuilder>().InstancePerDependency();
        builder.RegisterType<BugReportLogValidator>().As<IBugReportLogValidator>().InstancePerDependency();
        builder.RegisterType<BugReportUploader>().As<IBugReportUploader>().InstancePerDependency();
        builder.RegisterType<BugReportLogSharingPreference>().As<IBugReportLogSharingPreference>().InstancePerDependency();
        builder.RegisterType<BugReportSubmissionConsent>().As<IBugReportSubmissionConsent>().InstancePerDependency();
        builder.RegisterType<KillFeedOptionsTabProvider>().As<ICoopOptionsTabProvider>().InstancePerDependency();
        builder.RegisterType<MapTimeOptionsTabProvider>().As<ICoopOptionsTabProvider>().InstancePerDependency();
        builder.RegisterType<BugReportOptionsTabProvider>().As<ICoopOptionsTabProvider>().InstancePerDependency();
        builder.RegisterType<ChatOptionsTabProvider>().As<ICoopOptionsTabProvider>().InstancePerDependency();
        builder.RegisterType<PlayerNameplatesOptionsTabProvider>().As<ICoopOptionsTabProvider>().InstancePerDependency();
        builder.RegisterType<NetworkOptionsTabProvider>().As<ICoopOptionsTabProvider>().InstancePerDependency();
        builder.RegisterType<LocalMovementBandwidth>().As<ILocalMovementBandwidth>().InstancePerDependency();
        builder.RegisterType<ChatPlayerName>().As<IChatPlayerNameResolver>().InstancePerDependency();
        builder.RegisterType<PlayerPartyRestorer>().As<IPlayerPartyRestorer>().InstancePerDependency();
        builder.RegisterType<PlayerCreationRollback>().As<IPlayerCreationRollback>().InstancePerDependency();
        builder.RegisterType<MobilePartyBehaviorSnapshot>().As<IMobilePartyBehaviorSnapshot>().InstancePerDependency();
        builder.RegisterType<PartyAiBatchRunner>().As<IPartyAiBatchRunner>().InstancePerLifetimeScope().AutoActivate();
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
        builder.RegisterType<LocationConversationAgentGuard>().As<ILocationConversationAgentGuard>().InstancePerDependency();
        builder.RegisterType<BattleAgentBudget>().As<IBattleAgentBudget>().InstancePerDependency();
        builder.RegisterType<NearbyPartyReinforcer>().As<INearbyPartyReinforcer>().InstancePerDependency();
        builder.RegisterType<SiegeMapEventLeaderReconciler>().As<ISiegeMapEventLeaderReconciler>().InstancePerDependency();
        builder.RegisterType<AiSiegeAssaultReadiness>().As<IAiSiegeAssaultReadiness>().InstancePerDependency();
        builder.RegisterType<AiSiegeTerminalPolicy>().As<IAiSiegeTerminalPolicy>().InstancePerLifetimeScope();
        builder.RegisterType<SiegeEventGraphSynchronizer>().As<ISiegeEventGraphSynchronizer>().InstancePerDependency();
        builder.RegisterType<SiegeJoinMenuActivationGate>().As<ISiegeJoinMenuActivationGate>().InstancePerLifetimeScope();
        builder.RegisterType<MapEventContributionBarrier>().As<IMapEventContributionBarrier>().InstancePerDependency();
        builder.RegisterType<ArmyDisbander>().As<IArmyDisbander>().InstancePerDependency();
        builder.RegisterType<PlayerSiegeTargetScoring>().As<IPlayerSiegeTargetScoring>().InstancePerDependency();
        builder.RegisterType<ArmyFormationPositionConvergence>().As<IArmyFormationPositionConvergence>().InstancePerDependency();
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
        builder.RegisterType<LocationNpcGateState>().As<ILocationNpcGate>().InstancePerLifetimeScope();
        builder.RegisterType<LocationConversationClientState>()
            .As<ILocationConversationClientState>()
            .InstancePerLifetimeScope();
        builder.RegisterType<SettlementHeroSpawnPool>()
            .As<ISettlementHeroSpawnPool>()
            .InstancePerDependency();
        builder.RegisterType<KingdomCreationSettlementTracker>().AsSelf().As<IKingdomCreationSettlementTracker>().InstancePerLifetimeScope();
        builder.RegisterType<KingdomCreator>().AsSelf().As<IKingdomCreator>().InstancePerLifetimeScope();
        builder.RegisterType<KingdomDecisionOutcomeResolver>().AsSelf().As<IKingdomDecisionOutcomeResolver>().InstancePerLifetimeScope();
        builder.RegisterType<KingdomDecisionOutcomeOrder>().AsSelf().As<IKingdomDecisionOutcomeOrder>().InstancePerDependency();
        builder.RegisterType<KingdomDecisionRoundPresentation>().AsSelf().As<IKingdomDecisionRoundPresentation>().InstancePerDependency();
        builder.RegisterType<KingdomDecisionVoteManager>().AsSelf().As<IKingdomDecisionVoteManager>().InstancePerLifetimeScope();
        builder.RegisterType<KingdomMembershipState>().AsSelf().As<IKingdomMembershipState>().InstancePerLifetimeScope();
        builder.RegisterType<ClientClanStrengthRefresher>().As<IClientClanStrengthRefresher>().InstancePerDependency();
        builder.RegisterType<MainPartyBattleRewardsCache>().As<IMainPartyBattleRewardsCache>().InstancePerLifetimeScope();
        builder.RegisterType<PacketManager>().As<IPacketManager>().InstancePerLifetimeScope();
        builder.RegisterType<MapEventInitializationBarrierBinding>().InstancePerLifetimeScope().AutoActivate();
        builder.RegisterType<MapTrackerProviderHolder>().As<IMapTrackerProviderHolder>().InstancePerLifetimeScope();

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
