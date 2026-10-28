using System;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

/// <summary>Holds client ending requests until its in-flight accept has been confirmed.</summary>
internal interface IQuestSolutionAcceptCoordinator
{
    void RunWhenAccepted(IssueBase issue, Action request);
}
