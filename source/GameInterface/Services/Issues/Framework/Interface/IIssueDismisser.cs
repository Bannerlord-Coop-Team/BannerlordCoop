using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IIssueDismisser
{
    // Ends the issue without running any vanilla outcome, freeing companions it sent as troops
    void Dismiss(IssueBase issue);

    // Before every server troop return: notes it on the issue's claim and drops sent companions whose death already ran
    // without making them Dead, the way vanilla's return drops dead ones
    void PrepareTroopReturn(IssueBase issue);

    // Whether the issue still has to be taken out of the manager
    bool IsInManager(IssueBase issue);

    // Takes an issue whose ending already ran out of the manager, a throw leaves it in
    void Remove(IssueBase issue);

    // Remove, and when finalizing throws deactivates it, which leaves a companion it sent disabled
    void ForceRemove(IssueBase issue);

    // Vanilla's troop return, as the main hero
    void ReturnTroops(IssueBase issue);

    // Adds vanilla's troops returned line unless the issue already has it
    void AddTroopsReturnedLog(IssueBase issue);

    // Vanilla's cancel update as the main hero, which ends the owner's open journal row and sends it to the owner's client
    void SendCancel(IssueBase issue);

    // Ends the issue's open journal row with its own lines, the issue's lines and the given ending
    void CloseJournal(IssueBase issue, IssueBase.IssueUpdateDetails ending);

    // Drops the issue's unfinished journal rows, ended history stays
    void DropUnfinishedJournal(IssueBase issue);
}
