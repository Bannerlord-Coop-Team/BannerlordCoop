using Common.Messaging;

namespace GameInterface.Services.Clans.Messages.Lifetime;

/// <summary>
/// Local event when a clan is created
/// </summary>
/// <param name="Data">Data for clan creation</param>
internal record ClanCreated(ClanCreatedData Data) : IEvent
{
    public ClanCreatedData Data { get; } = Data;
}
