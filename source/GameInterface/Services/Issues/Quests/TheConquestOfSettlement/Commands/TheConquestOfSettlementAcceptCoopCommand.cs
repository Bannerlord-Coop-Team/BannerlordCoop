using Common.Commands;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using Issue = TaleWorlds.CampaignSystem.Issues.TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue;

namespace GameInterface.Services.Issues.Quests.TheConquestOfSettlement.Commands;

internal sealed class TheConquestOfSettlementAcceptCoopCommand : ICoopCommand
{
    private readonly IObjectManager objectManager;

    public TheConquestOfSettlementAcceptCoopCommand(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public string Prefix => "coop.debug.issues.the_conquest_of_settlement";

    public string Name => "accept";

    public string Description => "Accepts The Conquest of Settlement the way the ruler's dialog does, after the server ran setup for this player.";

    // The accept has to start on the taker's client, the same as the dialog
    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = {
        new ExpectedArgs("hero_id", "The id of the ruler that setup printed."),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!objectManager.TryGetObject<Hero>(args[0], out var ruler))
        {
            return Failed($"Hero with id '{args[0]}' not found");
        }

        if (!(ruler.Issue is Issue issue) || !issue.IsOngoingWithoutQuest)
        {
            return Failed($"{ruler.Name} does not offer The Conquest of Settlement");
        }

        issue.StartIssueWithQuest();

        return new CoopCommandResult(true, $"Asked the server to accept The Conquest of Settlement from {ruler.Name}");
    }

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");
}
