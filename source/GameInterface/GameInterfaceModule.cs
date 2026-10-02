using GameInterface.Services.UI.CoopOptions.Providers.UITab;
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
using GameInterface.Services.Alleys;
using GameInterface.Services.Bandits;
using GameInterface.Services.Barters;
using GameInterface.Services.BugReporting;
using GameInterface.Services.Chat;
using GameInterface.Services.Entity;
using GameInterface.Services.GameDebug.Metrics;
using GameInterface.Services.Heroes;
using GameInterface.Services.Heroes.Commands;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Hideouts;
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
using GameInterface.Services.MapEvents.Participation;
using GameInterface.Services.MobileParties;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Commands;
using GameInterface.Services.MobilePartyAIs;
using GameInterface.Services.Modules;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party;
using GameInterface.Services.Players;
using GameInterface.Services.SiegeEvents;
using GameInterface.Services.SiegeEvents.Commands;
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
            .SingleInstance()
            .PreserveExistingDefaults();

        builder.RegisterType<SurrogateCollection>().As<ISurrogateCollection>().SingleInstance().AutoActivate();

        builder.RegisterType<CoopCommandArgsFactory>().As<ICoopCommandArgsFactory>().InstancePerDependency();
        builder.RegisterType<RglCommandLineRegistry>().As<IRglCommandLineRegistry>().InstancePerDependency();
        builder.RegisterType<CoopCommandRegistry>().As<ICoopCommandRegistry>().SingleInstance();
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
            .SingleInstance()
            .AutoActivate();

        builder.RegisterType<GameInterface>().As<IGameInterface>().SingleInstance().AutoActivate();
        // mod-config.json: one lazy read per session container (see IModConfig).
        builder.RegisterType<ModConfig>().As<IModConfig>().SingleInstance();
        builder.RegisterType<BinaryPackageFactory>().As<IBinaryPackageFactory>().SingleInstance();
        builder.RegisterType<ControllerIdProvider>().As<IControllerIdProvider>().SingleInstance();
        builder.RegisterType<TimeControlModeConverter>().As<ITimeControlModeConverter>().SingleInstance();
        builder.RegisterType<Services.Crime.CrimeRatingService>().As<Services.Crime.ICrimeRatingService>().InstancePerDependency();
        builder.RegisterType<PlayerManager>().As<IPlayerManager>().SingleInstance();
        builder.RegisterType<HideoutPreparation>().As<IHideoutPreparation>().InstancePerDependency();
        builder.RegisterType<BugReportService>().As<IBugReportService>().SingleInstance().AutoActivate();
        builder.RegisterType<BugReportOverlay>().As<IBugReportOverlay>().SingleInstance();
        builder.RegisterType<CoopLogSnapshotProvider>().As<ICoopLogSnapshotProvider>().InstancePerDependency();
        builder.RegisterType<BugReportServerSaveProvider>().As<IBugReportServerSaveProvider>().InstancePerDependency();
        builder.RegisterType<BugReportArchiveBuilder>().As<IBugReportArchiveBuilder>().InstancePerDependency();
        builder.RegisterType<BugReportLogValidator>().As<IBugReportLogValidator>().InstancePerDependency();
        builder.RegisterType<BugReportUploader>().As<IBugReportUploader>().InstancePerDependency();
        builder.RegisterType<BugReportLogSharingPreference>().As<IBugReportLogSharingPreference>().InstancePerDependency();
        builder.RegisterType<BugReportSubmissionConsent>().As<IBugReportSubmissionConsent>().InstancePerDependency();
        builder.RegisterType<UIOptionsTabProvider>().As<ICoopOptionsTabProvider>().InstancePerDependency();
        builder.RegisterType<NetworkOptionsTabProvider>().As<ICoopOptionsTabProvider>().InstancePerDependency();
        builder.RegisterType<LocalMovementBandwidth>().As<ILocalMovementBandwidth>().InstancePerDependency();
        builder.RegisterType<ChatPlayerName>().As<IChatPlayerNameResolver>().InstancePerDependency();
        builder.RegisterType<PlayerPartyRestorer>().As<IPlayerPartyRestorer>().InstancePerDependency();
        builder.RegisterType<PlayerCreationRollback>().As<IPlayerCreationRollback>().InstancePerDependency();
        builder.RegisterType<ServerPlayerUnstuck>().As<IServerPlayerUnstuck>().InstancePerDependency();
        builder.RegisterType<MobilePartyBehaviorSnapshot>().As<IMobilePartyBehaviorSnapshot>().InstancePerDependency();
        builder.RegisterType<PartyBehaviorWireMapper>().As<IPartyBehaviorWireMapper>().InstancePerDependency();
        builder.RegisterType<AlleyGarrisonData>().As<IAlleyGarrisonData>().InstancePerDependency();
#if DEBUG
        builder.RegisterType<ClanLordMovementFixture>().As<IClanLordMovementFixture>().SingleInstance();
        builder.RegisterType<ClanLordMovementFixtureRules>().As<IClanLordMovementFixtureRules>().InstancePerDependency();
#endif
        builder.RegisterType<PartyAiBatchRunner>().As<IPartyAiBatchRunner>().SingleInstance().AutoActivate();
        builder.RegisterType<BarterClientPresentation>().As<IBarterClientPresentation>().InstancePerDependency();
        builder.RegisterType<SafePassagePartyResolver>().AsSelf().As<ISafePassagePartyResolver>().InstancePerDependency();
        builder.RegisterType<PeacePursuitCleaner>().As<IPeacePursuitCleaner>().InstancePerDependency();
        builder.RegisterType<PartyVisibilitySweep>().As<IPartyVisibilitySweep>().InstancePerDependency();
        builder.RegisterType<ConversationRestartContextTracker>().As<IConversationRestartContextTracker>().SingleInstance();
        builder.RegisterType<IssueConversationTracker>().As<IIssueConversationTracker>().SingleInstance();
        builder.RegisterType<IssueOwnershipRegistry>().As<IIssueOwnershipRegistry>().SingleInstance();
        builder.RegisterType<IssueGenerationRegistry>().As<IIssueGenerationRegistry>().SingleInstance();
        builder.RegisterType<AwaitingAlternativeSolutionTroopsRegistry>().As<IAwaitingAlternativeSolutionTroopsRegistry>().SingleInstance();
        builder.RegisterType<BattleHostRegistry>().As<IBattleHostRegistry>().SingleInstance();
        builder.RegisterType<LocationHostRegistry>().As<ILocationHostRegistry>().SingleInstance();
        builder.RegisterType<LocationConversationAgentGuard>().As<ILocationConversationAgentGuard>().InstancePerDependency();
        builder.RegisterType<BattleAgentBudget>().As<IBattleAgentBudget>().InstancePerDependency();
        builder.RegisterType<NearbyPartyReinforcer>().As<INearbyPartyReinforcer>().InstancePerDependency();
#if DEBUG
        // One capture spans the independently resolved DEBUG commands for this campaign session.
        builder.RegisterType<DefenderFixtureBehaviorIdentity>().As<IDefenderFixtureBehaviorIdentity>().InstancePerDependency();
        builder.RegisterType<DefenderSiegeContextFixture>().As<IDefenderSiegeContextFixture>().SingleInstance();
        builder.RegisterType<DefenderFixtureCaptivityActions>().As<IDefenderFixtureCaptivityActions>().InstancePerDependency();
#endif
        builder.RegisterType<SiegeMapEventLeaderReconciler>().As<ISiegeMapEventLeaderReconciler>().InstancePerDependency();
        builder.RegisterType<AiSiegeAssaultReadiness>().As<IAiSiegeAssaultReadiness>().InstancePerDependency();
        builder.RegisterType<SiegeDefenderCommandAuthority>().As<ISiegeDefenderCommandAuthority>().InstancePerDependency();
        builder.RegisterType<AiSiegeTerminalPolicy>().As<IAiSiegeTerminalPolicy>().SingleInstance();
        builder.RegisterType<SiegeEventGraphSynchronizer>().As<ISiegeEventGraphSynchronizer>().InstancePerDependency();
        builder.RegisterType<SiegeJoinMenuActivationGate>().As<ISiegeJoinMenuActivationGate>().SingleInstance();
        builder.RegisterType<MapEventContributionBarrier>().As<IMapEventContributionBarrier>().InstancePerDependency();
        builder.RegisterType<SiegeBreakOut>().As<ISiegeBreakOut>().InstancePerDependency();
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
        builder.RegisterType<MapEventLogger>().As<IMapEventLogger>().SingleInstance();
        builder.RegisterType<TroopRosterLogger>().As<ITroopRosterLogger>().SingleInstance();
        builder.RegisterType<PartySyncPerformanceClock>().As<IPartySyncPerformanceClock>().SingleInstance();
        builder.RegisterType<PartySyncPerformanceFileWriter>().As<IPartySyncPerformanceFileWriter>().SingleInstance();
        builder.RegisterType<PartySyncPerformancePartyProvider>().As<IPartySyncPerformancePartyProvider>().SingleInstance();
        builder.RegisterType<LiveTestCommandDispatcher>().As<ILiveTestCommandDispatcher>().InstancePerDependency();
        builder.RegisterType<CoopModulePathResolver>().As<ICoopModulePathResolver>().InstancePerDependency();
        builder.RegisterType<FixedTownNpcService>().AsSelf().SingleInstance();
        builder.RegisterType<LocationNpcGateState>().As<ILocationNpcGate>().SingleInstance();
        builder.RegisterType<LocationConversationClientState>()
            .As<ILocationConversationClientState>()
            .SingleInstance();
        builder.RegisterType<SettlementHeroSpawnPool>()
            .As<ISettlementHeroSpawnPool>()
            .InstancePerDependency();
        builder.RegisterType<KingdomCreationSettlementTracker>().AsSelf().As<IKingdomCreationSettlementTracker>().SingleInstance();
        builder.RegisterType<KingdomCreator>().AsSelf().As<IKingdomCreator>().SingleInstance();
        builder.RegisterType<KingdomDecisionOutcomeResolver>().AsSelf().As<IKingdomDecisionOutcomeResolver>().SingleInstance();
        builder.RegisterType<KingdomDecisionOutcomeOrder>().AsSelf().As<IKingdomDecisionOutcomeOrder>().InstancePerDependency();
        builder.RegisterType<KingdomDecisionRoundPresentation>().AsSelf().As<IKingdomDecisionRoundPresentation>().InstancePerDependency();
        builder.RegisterType<KingdomDecisionVoteManager>().AsSelf().As<IKingdomDecisionVoteManager>().SingleInstance();
        builder.RegisterType<KingdomMembershipState>().AsSelf().As<IKingdomMembershipState>().SingleInstance();
        builder.RegisterType<OfflineWarProtection>().As<IOfflineWarProtection>().InstancePerDependency();
        builder.RegisterType<ClientClanStrengthRefresher>().As<IClientClanStrengthRefresher>().InstancePerDependency();
        builder.RegisterType<MainPartyBattleRewardsCache>().As<IMainPartyBattleRewardsCache>().SingleInstance();
        builder.RegisterType<HideoutResultEncounter>().As<IHideoutResultEncounter>().InstancePerDependency();
        builder.RegisterType<PacketManager>().As<IPacketManager>().SingleInstance();
        builder.RegisterType<MapEventInitializationBarrierBinding>().SingleInstance().AutoActivate();
        builder.RegisterType<RetreatedMapEventPartyTracker>().As<IRetreatedMapEventPartyTracker>().SingleInstance();
        builder.RegisterType<MapTrackerProviderHolder>().As<IMapTrackerProviderHolder>().SingleInstance();

#if DEBUG
        builder.RegisterModule<global::GameInterface.Services.LiveTesting.LiveTestUiModule>();
#endif
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
