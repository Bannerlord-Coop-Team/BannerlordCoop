namespace GameInterface.Services.Issues.Framework.Interface;

public interface IIssueFinalizationReplay
{
    void ReplayLocalCancel(string issueOwnerId, string issueId, bool wasOwner);
}
