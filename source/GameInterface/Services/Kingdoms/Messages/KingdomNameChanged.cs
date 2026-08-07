using Common.Messaging;

namespace GameInterface.Services.Kingdoms.Messages;

/// <summary>
/// Raised after the server successfully applies a kingdom name change
/// </summary>
public record KingdomNameChanged : IEvent
{
    public string ControllerId { get; }
    public string KingdomId { get; }

    public KingdomNameChanged(string controllerId, string kingdomId)
    {
        ControllerId = controllerId;
        KingdomId = kingdomId;
    }
}