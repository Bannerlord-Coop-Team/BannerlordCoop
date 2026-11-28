using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Registries;

/// <summary>What one started ending of an issue already did, see <see cref="IssueEndingClaimRegistry"/>.</summary>
public sealed class IssueEndingClaim
{
    // The first troop ending published for the owner, which the owner's journal row keeps
    public IssueBase.IssueUpdateDetails? SentEnding { get; internal set; }

    // The issue's troop return ran, so the troops are never returned a second time
    public bool ReturnStarted { get; internal set; }
}
