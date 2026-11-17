namespace GameInterface.Services.Issues.Framework.Interface;

internal interface IIssueFinalizationReplay
{
    void ReplayLocalCancel(string issueOwnerId, string issueId, bool wasOwner);
}
