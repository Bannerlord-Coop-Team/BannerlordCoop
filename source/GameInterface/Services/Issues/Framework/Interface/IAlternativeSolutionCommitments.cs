using GameInterface.Services.TroopRosters.Data;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IAlternativeSolutionCommitments
{
    TroopRosterElementData[] Consume(string heroId, string issueOwnerId, string issueId, TroopRosterElementData[] requested);
}

public interface ICommittedAlternativeSolutionAcceptStrategy
{
}
