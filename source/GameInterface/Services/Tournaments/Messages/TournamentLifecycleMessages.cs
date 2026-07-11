using Common.Messaging;

namespace GameInterface.Services.Tournaments.Messages;

/// <summary>
/// Requests synchronous teardown of non-persistable tournament sessions before an orderly server save.
/// </summary>
public sealed class TournamentOrderlyShutdownRequested : IEvent
{
}
