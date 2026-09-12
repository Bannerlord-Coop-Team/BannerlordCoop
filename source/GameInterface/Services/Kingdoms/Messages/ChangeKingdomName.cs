using Common.Messaging;

namespace GameInterface.Services.Kingdoms.Messages;

/// <summary>
/// Commands the gamee interface to validate and
/// apply a kingdom name change for an authenticated player
/// </summary>
public record ChangeKingdomName : ICommand
{
    public string ControllerId { get; }
    public string KingdomId { get; }
    public string Name { get; }

    public ChangeKingdomName(string controllerId, string kingdomId, string name)
    {
        ControllerId = controllerId;
        KingdomId = kingdomId;
        Name = name;
    }
}