using Common.Commands;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Issue = TaleWorlds.CampaignSystem.Issues.ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArmyNeedsSupplies.Commands;

internal sealed class ArmyNeedsSuppliesStockCoopCommand : ICoopCommand
{
    private readonly IObjectManager objectManager;
    private readonly IIssueOwnerResolver ownerResolver;

    public ArmyNeedsSuppliesStockCoopCommand(IObjectManager objectManager, IIssueOwnerResolver ownerResolver)
    {
        this.objectManager = objectManager;
        this.ownerResolver = ownerResolver;
    }

    public string Prefix => "coop.debug.issues.army_needs_supplies";

    public string Name => "stock";

    public string Description => "Tops up the owning player's party with the grain, livestock and wine Rhagaea's accepted quest asks for.";

    public CoopCommandSide Side => CoopCommandSide.Server;

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!objectManager.TryGetObject<Hero>(ArmyNeedsSuppliesSetupCoopCommand.RhagaeaId, out var lord))
        {
            return Failed($"Rhagaea ({ArmyNeedsSuppliesSetupCoopCommand.RhagaeaId}) not found");
        }

        if (!(lord.Issue is Issue issue) || !(issue.IssueQuest is Quest quest))
        {
            return Failed($"{lord.Name} has no accepted Army Needs Supplies quest");
        }

        if (!ownerResolver.TryResolveOwnerHero(issue, out var owner))
        {
            return Failed($"Nobody owns the issue of {lord.Name}");
        }

        var roster = owner.PartyBelongedTo?.ItemRoster;
        if (roster == null)
        {
            return Failed($"{owner.Name} has no party");
        }

        var sheep = MBObjectManager.Instance.GetObject<ItemObject>("sheep");
        var wine = MBObjectManager.Instance.GetObject<ItemObject>("wine");

        var addedGrain = TopUp(roster, DefaultItems.Grain, quest._requestedGrainAmount, roster.GetItemNumber(DefaultItems.Grain));
        var addedSheep = TopUp(roster, sheep, quest._requestedLiveStockAmount, roster.NumberOfLivestockAnimals);
        var addedWine = TopUp(roster, wine, quest._requestedWineAmount, roster.GetItemNumber(wine));

        return new CoopCommandResult(true,
            $"Added {addedGrain} grain, {addedSheep} sheep and {addedWine} wine to {owner.Name}'s party. " +
            $"The quest asks for {quest._requestedGrainAmount} grain, {quest._requestedLiveStockAmount} livestock and {quest._requestedWineAmount} wine.");
    }

    private static int TopUp(ItemRoster roster, ItemObject item, int requested, int carried)
    {
        var missing = requested - carried;
        if (missing <= 0)
        {
            return 0;
        }

        roster.AddToCounts(item, missing);
        return missing;
    }

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");
}
