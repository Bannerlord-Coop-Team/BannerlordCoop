using GameInterface.Services.Issues.Framework.Registries;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

/// <summary>Server: the issues whose one ending already started, so none of them is paid or returned twice.</summary>
public interface IIssueEndingClaimRegistry
{
    // Taken before a sent troops cancel or completion runs vanilla's consequences and troop return. False when one
    // already started, so it must not run again, IActiveIssueDismisser.EndAfterThrow finishes it.
    bool TryClaim(IssueBase issue);

    // The issue's claim, taken now if nothing claimed it yet
    IssueEndingClaim GetOrClaim(IssueBase issue);

    // Every troop return notes it on the issue's claim, so the troops are never returned again
    void NoteTroopReturn(IssueBase issue);
}
