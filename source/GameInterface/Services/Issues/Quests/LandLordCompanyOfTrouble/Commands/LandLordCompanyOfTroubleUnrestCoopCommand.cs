using Common.Commands;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using Issue = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble.Commands;

internal sealed class LandLordCompanyOfTroubleUnrestCoopCommand : ICoopCommand
{
    private readonly IObjectManager objectManager;
    private readonly IIssueOwnerResolver ownerResolver;

    public LandLordCompanyOfTroubleUnrestCoopCommand(IObjectManager objectManager, IIssueOwnerResolver ownerResolver)
    {
        this.objectManager = objectManager;
        this.ownerResolver = ownerResolver;
    }

    public string Prefix => "coop.debug.issues.company_of_trouble";

    public string Name => "unrest";

    public string Description => "Removes the owning player's other troops until Rhagaea's mercenaries are half the party, so they ask to leave on the next hour.";

    public CoopCommandSide Side => CoopCommandSide.Server;

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!objectManager.TryGetObject<Hero>(LandLordCompanyOfTroubleSetupCoopCommand.RhagaeaId, out var lord))
        {
            return Failed($"Rhagaea ({LandLordCompanyOfTroubleSetupCoopCommand.RhagaeaId}) not found");
        }

        if (!(lord.Issue is Issue issue) || !(issue.IssueQuest is Quest quest))
        {
            return Failed($"{lord.Name} has no accepted Company of Trouble quest");
        }

        if (!ownerResolver.TryResolveOwnerHero(issue, out var owner))
        {
            return Failed($"Nobody owns the issue of {lord.Name}");
        }

        var roster = owner.PartyBelongedTo.MemberRoster;
        var mercenaries = roster.GetTroopCount(quest._troubleCharacterObject);
        if (mercenaries == 0)
        {
            return Failed($"{owner.Name}'s party has no mercenaries left");
        }

        var removed = 0;
        foreach (var element in roster.GetTroopRoster().ToList())
        {
            if (roster.TotalManCount - mercenaries <= mercenaries)
            {
                break;
            }

            if (element.Character.IsHero || element.Character == quest._troubleCharacterObject)
            {
                continue;
            }

            roster.AddToCounts(element.Character, -element.Number, woundedCount: -element.WoundedNumber);
            removed += element.Number;
        }

        if (roster.TotalManCount - mercenaries > mercenaries)
        {
            return Failed($"Removed {removed} troops, but {owner.Name}'s heroes still outnumber the {mercenaries} mercenaries");
        }

        return new CoopCommandResult(true,
            $"Removed {removed} troops from {owner.Name}'s party, {mercenaries} mercenaries remain. " +
            "Stay on the map outside any settlement or battle and the mercenaries ask to leave on the next hour.");
    }

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");
}
