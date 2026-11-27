using GameInterface.Services.Issues.Framework.Registries;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IIssueOwnershipRegistry
{
    // First controller to claim an issue wins, false if another controller already owns it
    bool TrySetOwner(string issueOwnerId, string issueId, string controllerId);

    bool TryGetOwner(string issueOwnerId, string issueId, out string controllerId);

    bool TryGetIssueOwnerId(string issueId, out string issueOwnerId);

    void Remove(string issueOwnerId);

    // Removes the giver's entry only while it names this issue, so a late ending keeps the owner of the giver's next issue
    void Remove(string issueOwnerId, string issueId);

    IssueOwnershipData[] GetAll();
}
