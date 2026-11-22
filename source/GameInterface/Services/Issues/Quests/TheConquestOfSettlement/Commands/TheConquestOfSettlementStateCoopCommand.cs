using Common.Commands;
using GameInterface.Services.ObjectManager;
using System.Linq;
using TaleWorlds.CampaignSystem;
using Issue = TaleWorlds.CampaignSystem.Issues.TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest;

namespace GameInterface.Services.Issues.Quests.TheConquestOfSettlement.Commands;

internal sealed class TheConquestOfSettlementStateCoopCommand : ICoopCommand
{
    private readonly IObjectManager objectManager;

    public TheConquestOfSettlementStateCoopCommand(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public string Prefix => "coop.debug.issues.the_conquest_of_settlement";

    public string Name => "state";

    public string Description => "Reports a ruler's The Conquest of Settlement issue and the conquest quests in this machine's quest manager.";

    public CoopCommandSide Side => CoopCommandSide.Both;

    public IExpectedArgs[] ExpectedArgs { get; } = {
        new ExpectedArgs("hero_id", "The id of the ruler that setup printed."),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!objectManager.TryGetObject<Hero>(args[0], out var ruler))
        {
            return new CoopCommandResult(false, $"Hero with id '{args[0]}' not found", "command_failed");
        }

        var issue = ruler.Issue as Issue;
        var issueState = issue == null ? "none" : $"{issue.StringId} {issue._issueState}, quest {(issue.IssueQuest == null ? "none" : issue.IssueQuest.IsOngoing ? "ongoing" : "ended")}";

        // _quests, because the Quests getter hides other players' conquest quests on clients
        var quests = Campaign.Current.QuestManager._quests
            .OfType<Quest>()
            .Select(quest => $"{quest.StringId} giver={quest.QuestGiver?.StringId} ongoing={quest.IsOngoing}")
            .ToList();

        return new CoopCommandResult(true, $"Issue: {issueState}. Conquest quests: {(quests.Count == 0 ? "none" : string.Join(", ", quests))}");
    }
}
