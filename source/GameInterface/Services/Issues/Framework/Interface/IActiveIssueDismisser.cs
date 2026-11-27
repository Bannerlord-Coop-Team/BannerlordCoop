namespace GameInterface.Services.Issues.Framework.Interface;

/// <summary>Server: the sweep that ends the issues of players who are gone.</summary>
public interface IActiveIssueDismisser
{
    // Ends every registered issue being solved whose player is disconnected, unknown or the removed controller (null when
    // nobody was removed) through IIssueEndingCoordinator.EndWithoutOwner. Startup passes announce false.
    void EndIssuesOfGonePlayers(bool announce, string removedControllerId);
}
