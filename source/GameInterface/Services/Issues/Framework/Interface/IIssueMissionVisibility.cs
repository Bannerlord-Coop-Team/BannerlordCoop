using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

internal interface IIssueMissionVisibility
{
    bool IsAnotherPlayersTroopsMission(IssueBase issue);
}
