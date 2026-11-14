using GameInterface.Services.Issues.Framework.AcceptCoordination;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

internal interface ILordSolutionCoordinator
{
    bool Before(IssueBase issue, LordSolutionOperation operation);

    /// <summary>The owner's counter-offer decision is waiting for the server, so the merchant must not ask again.</summary>
    bool IsDecisionPending(IssueBase issue);
}
