using Common.Commands;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Commands;

public sealed class IssuesGiveCoopCommand : ICoopCommand
{
    private readonly IQuestTypeRegistry registry;
    private readonly IObjectManager objectManager;

    public IssuesGiveCoopCommand(IQuestTypeRegistry registry, IObjectManager objectManager)
    {
        this.registry = registry;
        this.objectManager = objectManager;
    }

    public string Prefix => "coop.debug.issues";

    public string Name => "give";

    public string Description => "Gives a hero a registered quest type, skipping the quest's own eligibility checks.";

    public CoopCommandSide Side => CoopCommandSide.Server;

    public IExpectedArgs[] ExpectedArgs { get; } = {
        new ExpectedArgs("hero_id", "The registered id of the hero that offers the issue."),
        new ExpectedArgs("quest_type", "The issue type name, see coop.debug.issues.list_types."),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!objectManager.TryGetObject<Hero>(args[0], out var hero))
        {
            return Failed($"Hero with id '{args[0]}' not found");
        }

        if (!registry.TryGetByName(args[1], out var descriptor))
        {
            return Failed($"Quest type '{args[1]}' is not registered");
        }

        if (!hero.IsNotable)
        {
            return Failed($"{hero.Name} is not a notable, only notables can offer issues");
        }

        if (hero.Issue != null)
        {
            return Failed($"{hero.Name} already has the issue {hero.Issue.GetType().Name}");
        }

        if (hero.CurrentSettlement == null)
        {
            return Failed($"{hero.Name} is not in a settlement");
        }

        var strategy = descriptor.CreationCaptureStrategy;

        if (!strategy.TryBuildDebugCapture(hero, out var captured))
        {
            return Failed($"No default values could be found for {descriptor.IssueType.Name}");
        }

        var potentialIssue = new PotentialIssueData(
            (in PotentialIssueData _, Hero owner) => strategy.CreateIssue(owner, captured),
            descriptor.IssueType,
            IssueBase.IssueFrequency.Common);

        try
        {
            Campaign.Current.IssueManager.CreateNewIssue(in potentialIssue, hero);
        }
        catch (Exception e)
        {
            hero.Issue?.IssueFinalized();

            return Failed($"Creating {descriptor.IssueType.Name} failed: {e.Message}");
        }

        return new CoopCommandResult(true, $"{hero.Name} now offers {descriptor.IssueType.Name}");
    }

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");
}
