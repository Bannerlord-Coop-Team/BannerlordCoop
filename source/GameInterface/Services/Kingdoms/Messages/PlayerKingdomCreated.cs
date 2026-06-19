using Common.Messaging;

namespace GameInterface.Services.Kingdoms.Messages;

public record PlayerKingdomCreated : IEvent
{
    public string ControllerId { get; }
    public string KingdomId { get; }
    public string KingdomName { get; }
    public string ClanId { get; }

    public PlayerKingdomCreated(string controllerId, string kingdomId, string kingdomName, string clanId)
    {
        ControllerId = controllerId;
        KingdomId = kingdomId;
        KingdomName = kingdomName;
        ClanId = clanId;
    }
}
