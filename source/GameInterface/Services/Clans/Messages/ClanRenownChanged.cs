using Common.Messaging;

namespace GameInterface.Services.Clans.Messages;

/// <summary>
/// Local event raised on the server when a clan's renown changes, so it can be replicated to clients.
/// </summary>
public record ClanRenownChanged : IEvent
{
    public string ClanId { get; }
    public float Renown { get; }

    public ClanRenownChanged(string clanId, float renown)
    {
        ClanId = clanId;
        Renown = renown;
    }
}
