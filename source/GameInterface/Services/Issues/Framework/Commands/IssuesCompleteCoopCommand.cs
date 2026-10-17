using Common.Commands;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Framework.Commands;

public sealed class IssuesCompleteCoopCommand : ICoopCommand
{
    private readonly IObjectManager objectManager;
    private readonly IIssueOwnerResolver ownerResolver;

    public IssuesCompleteCoopCommand(IObjectManager objectManager, IIssueOwnerResolver ownerResolver)
    {
        this.objectManager = objectManager;
        this.ownerResolver = ownerResolver;
    }

    public string Prefix => "coop.debug.issues";

    public string Name => "complete";

    public string Description => "Ends a hero's issue with an outcome.";

    public CoopCommandSide Side => CoopCommandSide.Server;

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("hero_id", "The registered id of the hero that offers the issue."),
        new ExpectedArgs("outcome", "success, fail, betrayal, cancel or timeout. Success is the default.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!objectManager.TryGetObject<Hero>(args[0], out var hero))
        {
            return Failed($"Hero with id '{args[0]}' not found");
        }

        var issue = hero.Issue;
        if (issue == null)
        {
            return Failed($"{hero.Name} has no issue");
        }

        var outcome = args.Count > 1 ? args[1].Trim().ToLowerInvariant() : "success";
        var quest = issue.IssueQuest;

        // The server runs the ending as the player that owns the issue
        IDisposable owner = null;
        if (ownerResolver.TryResolveOwnerHero(issue, out var ownerHero))
        {
            owner = new MainHeroSubstitutionScope(ownerHero, ownerHero.PartyBelongedTo);
        }

        using (owner)
        {
            if (quest != null)
            {
                switch (outcome)
                {
                    case "success":
                        quest.CompleteQuestWithSuccess();
                        break;
                    case "fail":
                        quest.CompleteQuestWithFail();
                        break;
                    case "betrayal":
                        quest.CompleteQuestWithBetrayal();
                        break;
                    case "cancel":
                        quest.CompleteQuestWithCancel();
                        break;
                    case "timeout":
                        quest.CompleteQuestWithTimeOut();
                        break;
                    default:
                        return Failed($"Unknown outcome '{outcome}'");
                }
            }
            else
            {
                switch (outcome)
                {
                    case "timeout":
                        issue.CompleteIssueWithTimedOut();
                        break;
                    case "cancel":
                        issue.CompleteIssueWithStayAliveConditionsFailed();
                        break;
                    default:
                        return Failed($"{hero.Name} has no accepted quest, only timeout and cancel can end the issue");
                }
            }
        }

        return new CoopCommandResult(true, $"Ended the issue of {hero.Name} with {outcome}");
    }

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");
}
