using GameInterface.Services.Issues.Framework.Registries;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

/// <summary>Server: what each sent troops ending already did, so an ending that threw never repeats it.</summary>
public interface IIssueEndingClaimRegistry
{
    // The issue's claim, taken now if nothing noted anything for it yet
    IssueEndingClaim Get(IssueBase issue);

    // Every troop return notes it on the issue's claim, so the troops are never returned again
    void NoteTroopReturn(IssueBase issue);
}
