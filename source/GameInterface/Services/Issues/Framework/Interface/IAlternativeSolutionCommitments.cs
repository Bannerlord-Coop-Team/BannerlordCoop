using GameInterface.Services.TroopRosters.Data;
using LiteNetLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IAlternativeSolutionCommitments
{
    void ReturnForIssue(string issueOwnerId, string issueId);
    IReadOnlyCollection<TroopRosterElement> GetSelection(NetPeer requester, string issueOwnerId, string issueId);
    // Replaces the troops held for a player's quest picker with what their party screen commit left there, and tells them.
    void SetSelection(NetPeer requester, string issueOwnerId, string issueId, IEnumerable<TroopRosterElement> troops);
    TroopRosterElementData[] Consume(string heroId, string issueOwnerId, string issueId, TroopRosterElementData[] requested);
}

