using Common.Messaging;

namespace Missions.Messages;

/// <summary>The local battle controller is being disposed, including aborted missions.</summary>
public record BattleMissionEnded(string MapEventId) : IEvent;
