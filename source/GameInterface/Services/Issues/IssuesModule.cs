using Autofac;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Dispatch;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Registries;
using GameInterface.Services.Issues.Framework.Resolvers;
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
        builder.RegisterType<QuestTypeRegistry>().As<IQuestTypeRegistry>().InstancePerLifetimeScope();
        builder.RegisterType<IssueResolver>().As<IIssueResolver>().InstancePerLifetimeScope();
        builder.RegisterType<IssueOwnerResolver>().As<IIssueOwnerResolver>().InstancePerLifetimeScope();
        builder.RegisterType<IssueOwnershipRegistry>().As<IIssueOwnershipRegistry>().InstancePerLifetimeScope();
        builder.RegisterType<AlternativeSolutionTroopValidator>().As<IAlternativeSolutionTroopValidator>().InstancePerDependency();
        builder.RegisterType<OutcomeDispatcher>().As<IOutcomeDispatcher>().InstancePerLifetimeScope();

        builder.RegisterAssemblyTypes(typeof(IssuesModule).Assembly)
            .Where(IsQuestPiece)
            .AsSelf()
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();

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
