namespace GameInterface.Services.Issues.Framework.Interface;

public interface IIssueFinalizationReplay
{
    // Replays a local cancellation dropped from a join snapshot after capturing ownership before replacement.
    void ReplayLocalCancel(string issueOwnerId, string issueId, bool wasOwner);
}
