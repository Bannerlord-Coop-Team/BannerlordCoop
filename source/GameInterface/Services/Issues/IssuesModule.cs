using Autofac;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Conversation;
using GameInterface.Services.Issues.Framework.Dismissal;
using GameInterface.Services.Issues.Framework.Dispatch;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Gating;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Registries;
using GameInterface.Services.Issues.Framework.Resolvers;
using GameInterface.Services.Issues.Quests.CaravanAmbush;
using GameInterface.Services.Issues.Quests.ArtisanProducts;
using GameInterface.Services.Issues.Quests.LadysKnightOut;
using GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd;
using GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws;
using GameInterface.Services.Issues.Quests.LesserNobleRevolt;
using GameInterface.Services.Issues.Quests.TheConquestOfSettlement;
using GameInterface.Services.Issues.Quests.VillageNeedsTools;
using System;
using GameInterface.Services.Issues.Quests.RevenueFarming;
using GameInterface.Services.Issues.Quests.EscortMerchantCaravan;

namespace GameInterface.Services.Issues;

/// <summary>
/// Dependancy injection registration for issues & quests
/// </summary>
internal class IssuesModule : Module
{
    private const string QuestsNamespace = "GameInterface.Services.Issues.Quests";

    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<QuestFeatureFlag>().As<IQuestFeatureFlag>().InstancePerDependency();
        builder.RegisterType<QuestTypeRegistry>().As<IQuestTypeRegistry>().SingleInstance();
        builder.RegisterType<IssueResolver>().As<IIssueResolver>().SingleInstance();
        builder.RegisterType<IssueOwnerResolver>().As<IIssueOwnerResolver>().SingleInstance();
        builder.RegisterType<IssueOwnershipRegistry>().As<IIssueOwnershipRegistry>().SingleInstance();
        builder.RegisterType<AlternativeSolutionTroopValidator>().As<IAlternativeSolutionTroopValidator>().InstancePerDependency();
        builder.Register(context => context.Resolve<AlternativeSolutionCommitments>()).As<IAlternativeSolutionCommitments>();
        builder.Register(context => context.Resolve<AlternativeSolutionPicker>()).As<IAlternativeSolutionPicker>();
        builder.RegisterType<IssueAcceptPreconditions>().As<IIssueAcceptPreconditions>().InstancePerDependency();
        builder.RegisterType<ArtisanProductsProgress>().As<IArtisanProductsProgress>().InstancePerDependency();
        builder.RegisterType<OutcomeDispatcher>().As<IOutcomeDispatcher>().SingleInstance();
        builder.Register(context => context.Resolve<CaravanAmbushRewardConversation>()).As<ICaravanAmbushRewardConversation>();
        builder.RegisterType<CaravanAmbushConversationMap>().As<ICaravanAmbushConversationMap>().InstancePerDependency();
        builder.RegisterType<CaravanAmbushProgress>().As<ICaravanAmbushProgress>().InstancePerLifetimeScope();
        builder.RegisterType<CaravanAmbushWorldEventService>().As<ICaravanAmbushWorldEventService>().InstancePerLifetimeScope();
        builder.RegisterType<HeadmanHerdCleanup>().As<IHeadmanHerdCleanup>().SingleInstance();
        builder.RegisterType<IssueCancellationCoordinator>().As<IIssueCancellationCoordinator>().InstancePerLifetimeScope();
        builder.Register(context => context.Resolve<ActiveIssueDismisser>()).As<IActiveIssueDismisser>();
        builder.RegisterType<IssueConversationTracker>().As<IIssueConversationTracker>().SingleInstance();
        builder.RegisterType<IssueConversationGuard>().As<IIssueConversationGuard>().SingleInstance();
        builder.RegisterType<MerchantOutlawsProgress>().As<IMerchantOutlawsProgress>().InstancePerLifetimeScope();
        builder.RegisterType<MerchantOutlawsHourlyTick>().As<IMerchantOutlawsHourlyTick>().InstancePerDependency();
        builder.Register(context => context.Resolve<LordSolutionCoordinator>()).As<ILordSolutionCoordinator>();
        builder.Register(context => context.Resolve<FinalizationCoordinator>()).As<IIssueFinalizationReplay>();
        builder.Register(context => context.Resolve<QuestSolutionAcceptCoordinator>()).As<IQuestSolutionAcceptCoordinator>();
        builder.RegisterType<TheConquestOfSettlementService>()
            .As<ITheConquestOfSettlementService>()
            .SingleInstance();
        builder.RegisterType<LesserNobleRevoltWorldEventService>()
            .As<ILesserNobleRevoltWorldEventService>()
            .SingleInstance();
        builder.RegisterType<EscortProgress>().As<IEscortProgress>().InstancePerDependency();
        builder.RegisterType<EscortOwnerParty>().As<IEscortOwnerParty>().InstancePerDependency();
        builder.RegisterType<QuestEventRelay>().As<IQuestEventRelay>().InstancePerDependency();
        builder.RegisterType<VillageNeedsToolsService>().As<IVillageNeedsToolsService>().InstancePerDependency();
        builder.RegisterType<LadysKnightOutTournamentProgress>().As<ILadysKnightOutTournamentProgress>().InstancePerLifetimeScope();
        builder.RegisterType<IssueDismisser>().As<IIssueDismisser>().InstancePerDependency();
        builder.RegisterType<TextObjectComparer>().As<ITextObjectComparer>().InstancePerDependency();

        builder.RegisterAssemblyTypes(typeof(IssuesModule).Assembly)
            .Where(IsQuestPiece)
            .AsSelf()
            .AsImplementedInterfaces()
            .SingleInstance();

        builder.RegisterType<RevenueFarmingSessionStore>().As<IRevenueFarmingSessionStore>().InstancePerLifetimeScope();
        builder.RegisterType<RevenueFarmingExecutionScope>().As<IRevenueFarmingExecutionScope>().InstancePerLifetimeScope();
        builder.RegisterType<RevenueFarmingActions>().As<IRevenueFarmingActions>().InstancePerDependency();

        base.Load(builder);
    }

    private static bool IsQuestPiece(Type type)
    {
        if (!type.IsClass || type.IsAbstract || type.Namespace == null || !type.Namespace.StartsWith(QuestsNamespace))
        {
            return false;
        }

        return typeof(IQuestTypeDescriptor).IsAssignableFrom(type)
            || typeof(ICreationCaptureStrategy).IsAssignableFrom(type)
            || typeof(IQuestSolutionAcceptStrategy).IsAssignableFrom(type)
            || typeof(IAlternativeSolutionAcceptStrategy).IsAssignableFrom(type)
            || typeof(IFinalizationProofStrategy).IsAssignableFrom(type);
    }
}
