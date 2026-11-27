using Common.Messaging;

namespace GameInterface.Services.Issues.Framework.Visibility;

/// <summary>Published on the server after the issue manager's daily tick, for quests that send their state then.</summary>
public readonly struct IssueManagerDailyTicked : IEvent
{
}
