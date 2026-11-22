using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface ICreationCaptureStrategy
{
    // Server: read what the real constructor call produced
    byte[] Capture(IssueBase issue);

    // Client: build the issue with the captured constructor arguments
    IssueBase CreateIssue(Hero issueOwner, byte[] captured);

    // Client: force what vanilla recomputes after construction
    void ApplyAfterCreation(IssueBase issue, byte[] captured);

    // Debug give: pick default constructor arguments, skipping the quest's own eligibility checks
    bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured);
}
