using GameInterface.Services.TroopRosters.Data;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IAlternativeSolutionCommitments
{
    bool CanApply(string heroId, string issueOwnerId, string issueId, TroopRosterData delta);
    TroopRosterElementData[] Consume(string heroId, string issueOwnerId, string issueId, TroopRosterElementData[] requested);
}

public interface ICommittedAlternativeSolutionAcceptStrategy
{
}
