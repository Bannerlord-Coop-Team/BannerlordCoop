using Common.Messaging;

namespace GameInterface.Services.Clans.Messages.Lifetime;

/// <summary>
/// Local event when a clan is destroyed
/// </summary>
internal record ClanDestroyed(ClanDestroyedData Data) : IEvent
{
    public ClanDestroyedData Data { get; } = Data;
}