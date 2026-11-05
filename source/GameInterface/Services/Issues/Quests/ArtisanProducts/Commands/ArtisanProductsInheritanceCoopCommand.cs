using Common;
using Common.Commands;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.Commands;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;

namespace GameInterface.Services.Issues.Quests.ArtisanProducts.Commands;

/// <summary>Server fixture for an Artisan Products owner transfer when the native succession path cancels first.</summary>
internal sealed class ArtisanProductsInheritanceCoopCommand : ICoopCommand
{
    private readonly ICommandObjectResolver resolver;
    private readonly IObjectManager objectManager;

    public ArtisanProductsInheritanceCoopCommand(ICommandObjectResolver resolver, IObjectManager objectManager)
    {
        this.resolver = resolver;
        this.objectManager = objectManager;
    }

    public string Prefix => "coop.debug.artisan";
    public string Name => "inherit_issue";
    public string Description => "Moves an active Artisan Products issue through IssueManager.ChangeIssueOwner.";
    public CoopCommandSide Side => CoopCommandSide.Server;
    public IExpectedArgs[] ExpectedArgs { get; } =
    {
        new ExpectedArgs("old_owner_id", "Registered artisan issue owner hero id."),
        new ExpectedArgs("heir_id", "Registered living heir hero id."),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (ModInformation.IsClient) return Failed("Run this command on the server.");
        if (!resolver.TryResolveHero(args[0], out var oldOwner, out var oldError)) return Failed(oldError);
        if (!resolver.TryResolveHero(args[1], out var heir, out var heirError)) return Failed(heirError);
        if (oldOwner.Issue is not Issue issue) return Failed($"{oldOwner.Name} does not offer Artisan Products.");
        if (issue.IssueOwner != oldOwner) return Failed("The issue owner reference does not match old_owner_id.");
        if (!heir.IsAlive || heir.IsPrisoner || heir.IsFugitive || heir.IsReleased || heir.IsTraveling)
            return Failed($"Heir {heir.Name} must be living, free and present.");
        if (heir.Issue != null) return Failed($"Heir {heir.Name} already offers {heir.Issue.GetType().Name}.");
        if (!objectManager.TryGetId(issue, out var issueId) || !objectManager.TryGetId(oldOwner, out var oldId) ||
            !objectManager.TryGetId(heir, out var heirId)) return Failed("The issue or hero is not registered.");

        Campaign.Current.IssueManager.ChangeIssueOwner(issue, heir);
        if (issue.IssueOwner != heir || oldOwner.Issue != null || heir.Issue != issue)
            return Failed("IssueManager.ChangeIssueOwner did not produce the expected owner transfer.");
        return new CoopCommandResult(true, $"Artisan issue {issueId} moved from {oldId} to {heirId} through IssueManager.ChangeIssueOwner.");
    }

    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}
