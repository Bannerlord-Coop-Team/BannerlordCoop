using GameInterface.Services.Issues.Framework.Finalization;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

/// <summary>Releases an issue whose owning player cannot be resolved.</summary>
public interface IActiveIssueDismisser
{
    void Dismiss(IssueBase issue, IssueOutcome outcome);
}
