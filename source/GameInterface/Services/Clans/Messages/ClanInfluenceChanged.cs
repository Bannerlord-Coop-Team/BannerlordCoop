using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.Clans.Messages;

/// <summary>
/// Local event when a clan influence is changed from game interface
/// </summary>
[BatchLogMessage]
public record ClanInfluenceChanged : IEvent
{
    public string ClanId { get; }
    public float Amount { get; }

    public ClanInfluenceChanged(string clanId, float amount)
    {
        ClanId = clanId;
        Amount = amount;
    }
}