using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

internal interface IClientIssueDailyTickService
{
    void Apply(IssueManager issueManager);
}
