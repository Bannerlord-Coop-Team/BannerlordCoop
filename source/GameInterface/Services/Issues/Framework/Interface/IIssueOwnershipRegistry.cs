namespace GameInterface.Services.Issues.Framework.Interface;

public interface IIssueOwnershipRegistry
{
    // First controller to claim an issue wins, false if another controller already owns it
    bool TrySetOwner(string issueOwnerId, string issueId, string controllerId);

    bool TryGetOwner(string issueOwnerId, string issueId, out string controllerId);

    void Remove(string issueOwnerId);
}
