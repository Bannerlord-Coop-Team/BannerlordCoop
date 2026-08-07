using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages;

/// <summary>
/// Raised when the local player requests to rename a kingdom.
/// The server must validate and apply the request.
/// </summary>
public record KingdomNameChangeRequested : IEvent
{
    public Kingdom Kingdom { get; }
    public string Name { get; }

    public KingdomNameChangeRequested(Kingdom kingdom, string name)
    {
        Kingdom = kingdom;
        Name = name;
    }
}