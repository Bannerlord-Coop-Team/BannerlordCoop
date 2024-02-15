using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Event for when a new hero is synced across the network.
/// </summary>
public record NewHeroSynced : IEvent
{
}