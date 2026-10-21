using GameInterface.Services.TroopRosters.Data;
using LiteNetLib;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IAlternativeSolutionCommitments
{
    bool CanApply(NetPeer requester, string issueOwnerId, string issueId, TroopRosterData delta);
    TroopRosterElementData[] Consume(string heroId, string issueOwnerId, string issueId, TroopRosterElementData[] requested);
}

public interface ICommittedAlternativeSolutionAcceptStrategy
{
}
