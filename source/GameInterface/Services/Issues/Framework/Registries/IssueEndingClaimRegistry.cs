using GameInterface.Services.Issues.Framework.Interface;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Registries;

internal class IssueEndingClaimRegistry : IIssueEndingClaimRegistry
{
    // One claim per issue whose troop ending started, collected with the issue
    private readonly ConditionalWeakTable<IssueBase, IssueEndingClaim> claims = new();

    public IssueEndingClaim Get(IssueBase issue) => claims.GetValue(issue, _ => new IssueEndingClaim());

    public void NoteTroopReturn(IssueBase issue) => Get(issue).ReturnStarted = true;

    public void NoteSentEnding(IssueBase issue, IssueBase.IssueUpdateDetails ending) => Get(issue).SentEnding ??= ending;
}
