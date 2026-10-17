using Common.Commands;
using GameInterface.Services.Issues.Framework.Interface;
using System;
using System.Text;

namespace GameInterface.Services.Issues.Framework.Commands;

public sealed class IssuesListTypesCoopCommand : ICoopCommand
{
    private readonly IQuestTypeRegistry registry;

    public IssuesListTypesCoopCommand(IQuestTypeRegistry registry)
    {
        this.registry = registry;
    }

    public string Prefix => "coop.debug.issues";

    public string Name => "list_types";

    public string Description => "Lists every registered quest type.";

    public CoopCommandSide Side => CoopCommandSide.Both;

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        var output = new StringBuilder();
        output.AppendLine($"{registry.Descriptors.Count} registered quest types:");

        foreach (var descriptor in registry.Descriptors)
        {
            var alternative = descriptor.AlternativeSolutionAcceptStrategy != null ? "yes" : "no";
            var proof = descriptor.FinalizationProofStrategy != null ? "yes" : "no";
            output.AppendLine($"  {descriptor.IssueType.Name} (alternative solution: {alternative}, outcome proof: {proof})");
        }

        return new CoopCommandResult(true, output.ToString());
    }
}
