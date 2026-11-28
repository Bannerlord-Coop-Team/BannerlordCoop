using Common.Messaging;

namespace GameInterface.Services.AdvancedStart.Messages;

/// <summary>
/// Server: what a joining player may pick has changed, because the campaign clock started or a player took a kingdom,
/// so players still picking a start need the new policy.
/// </summary>
public record AdvancedStartPolicyChanged : IEvent;
