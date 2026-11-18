namespace GameInterface.Services.Issues.Framework.Interface;

public interface IIssueFinalizationReplay
{
    // Cancels the local copy of an issue the join snapshot no longer lists; wasOwner is this client's ownership from before the mirror was replaced.
    void ReplayLocalCancel(string issueOwnerId, string issueId, bool wasOwner);
}
