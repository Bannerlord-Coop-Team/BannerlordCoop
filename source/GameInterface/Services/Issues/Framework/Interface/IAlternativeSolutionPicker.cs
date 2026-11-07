using System;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IAlternativeSolutionPicker
{
    void Open(IssueBase issue);
    void Track(PartyScreenLogic logic);
    bool TryGetIssue(TroopRoster roster, out string giverId, out string issueId);
    void Close(IssueBase issue, bool cancel);

    /// <summary>Marks the screen PartyScreenHelper is closing, which pops it itself after the closed callback.</summary>
    IDisposable BeginNativeClose(PartyScreenLogic logic);
}
