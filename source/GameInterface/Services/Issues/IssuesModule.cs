using Autofac;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Conversation;
using GameInterface.Services.Issues.Framework.Dismissal;
using GameInterface.Services.Issues.Framework.Dispatch;
using GameInterface.Services.Issues.Framework.Gating;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Registries;
using GameInterface.Services.Issues.Framework.Resolvers;
using GameInterface.Services.Issues.Quests.LadysKnightOut;
using GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble;
using GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws;
using GameInterface.Services.Issues.Quests.LesserNobleRevolt;
using GameInterface.Services.Issues.Quests.TheConquestOfSettlement;
using GameInterface.Services.Issues.Quests.VillageNeedsTools;
using System;

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
        builder.RegisterType<OutcomeDispatcher>().As<IOutcomeDispatcher>().SingleInstance();
        builder.RegisterType<IssueConversationTracker>().As<IIssueConversationTracker>().SingleInstance();
        builder.RegisterType<MerchantOutlawsProgress>().As<IMerchantOutlawsProgress>().InstancePerLifetimeScope();
        builder.RegisterType<MerchantOutlawsHourlyTick>().As<IMerchantOutlawsHourlyTick>().InstancePerDependency();
        builder.Register(context => context.Resolve<LordSolutionCoordinator>()).As<ILordSolutionCoordinator>();
        builder.RegisterType<TheConquestOfSettlementService>()
            .As<ITheConquestOfSettlementService>()
            .SingleInstance();
        builder.RegisterType<LesserNobleRevoltWorldEventService>()
            .As<ILesserNobleRevoltWorldEventService>()
            .SingleInstance();
        builder.RegisterType<LandLordCompanyOfTroubleService>()
            .As<ILandLordCompanyOfTroubleService>()
            .InstancePerDependency();
        builder.RegisterType<QuestEventRelay>().As<IQuestEventRelay>().InstancePerDependency();
        builder.RegisterType<VillageNeedsToolsService>().As<IVillageNeedsToolsService>().InstancePerDependency();
        builder.RegisterType<LadysKnightOutTournamentProgress>().As<ILadysKnightOutTournamentProgress>().InstancePerLifetimeScope();
        builder.RegisterType<IssueDismisser>().As<IIssueDismisser>().InstancePerDependency();

        builder.RegisterAssemblyTypes(typeof(IssuesModule).Assembly)
            .Where(IsQuestPiece)
            .AsSelf()
            .AsImplementedInterfaces()
            .SingleInstance();

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
