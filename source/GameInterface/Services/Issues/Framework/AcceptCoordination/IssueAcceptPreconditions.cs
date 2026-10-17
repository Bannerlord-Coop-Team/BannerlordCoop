using Common.Logging;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

/// <summary>
/// Runs vanilla's checks for taking an issue on the server as the requesting player, so the server
/// turns down an accept that player's own conversation would have refused.
/// </summary>
internal class IssueAcceptPreconditions : IIssueAcceptPreconditions
{
    private static readonly ILogger Logger = LogManager.GetLogger<IssueAcceptPreconditions>();

    // The accept has already sent the troops away or started the quest by the time the server checks these
    private const IssueBase.PreconditionFlags AcceptChangedFlags =
        IssueBase.PreconditionFlags.NotEnoughTroops |
        IssueBase.PreconditionFlags.PartySizeLimit |
        IssueBase.PreconditionFlags.Money |
        IssueBase.PreconditionFlags.Influence;

    private readonly IIssueOwnerResolver ownerResolver;

    public IssueAcceptPreconditions(IIssueOwnerResolver ownerResolver)
    {
        this.ownerResolver = ownerResolver;
    }

    public bool CanTake(IssueBase issue, Hero playerHero, string controllerId)
    {
        try
        {
            if (HoldsSimilarIssue(issue, controllerId))
            {
                Logger.Information("Refused {issue} for {hero} because they already hold an issue of its type", issue.StringId, playerHero.StringId);
                return false;
            }

            using (new MainHeroSubstitutionScope(playerHero, playerHero.PartyBelongedTo))
            {
                if (!issue.IssueStayAliveConditions())
                {
                    Logger.Information("Refused {issue} for {hero} because the issue can no longer be solved", issue.StringId, playerHero.StringId);
                    return false;
                }

                var canTake = issue.CanPlayerTakeQuestConditions(issue.IssueOwner, out var flags, out _, out _, out _);
                if (!canTake && (flags == IssueBase.PreconditionFlags.None || (flags & ~AcceptChangedFlags) != IssueBase.PreconditionFlags.None))
                {
                    Logger.Information("Refused {issue} for {hero} because of {flags}", issue.StringId, playerHero.StringId, flags);
                    return false;
                }
            }

            return true;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Checking whether {hero} can take {issue} failed", playerHero.StringId, issue.StringId);
            return false;
        }
    }

    // Vanilla's similar commitments refusal, counting only the requesting player's issues
    private bool HoldsSimilarIssue(IssueBase issue, string controllerId)
    {
        if (issue.IssueQuestCanBeDuplicated)
        {
            return false;
        }

        foreach (var other in Campaign.Current.IssueManager.Issues.Values)
        {
            if (other != issue &&
                other.GetType() == issue.GetType() &&
                (other.IsSolvingWithQuest || other.IsSolvingWithAlternative) &&
                ownerResolver.TryGetOwnerControllerId(other, out var ownerControllerId) &&
                ownerControllerId == controllerId)
            {
                return true;
            }
        }

        return false;
    }
}
