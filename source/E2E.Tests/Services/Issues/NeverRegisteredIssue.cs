using TaleWorlds.CampaignSystem;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// An issue type of its own, so no quest type is ever registered for it. Tests that need an issue of an unregistered
/// type use this one instead of a vanilla issue, which stops being unregistered once its quest is ported.
/// </summary>
internal sealed class NeverRegisteredIssue : TaleWorlds.CampaignSystem.Issues.BettingFraudIssueBehavior.BettingFraudIssue
{
    public NeverRegisteredIssue(Hero issueOwner) : base(issueOwner)
    {
    }
}
