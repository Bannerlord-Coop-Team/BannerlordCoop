namespace GameInterface.Services.Issues.Framework.Finalization;

public enum IssueOutcome
{
    QuestSuccess = 0,
    QuestFail = 1,
    QuestBetrayal = 2,
    QuestCancel = 3,
    QuestTimeOut = 4,
    IssueTimedOut = 5,
    IssueStayAliveFailed = 6,
    AlternativeSolution = 7,
}
