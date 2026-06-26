using Common.Messaging;

namespace GameInterface.Services.Kingdoms.Messages;

public record CreateKingdom : ICommand
{
    public string ControllerId { get; }
    public string KingdomName { get; }
    public string CultureId { get; }

    public CreateKingdom(string controllerId, string kingdomName, string cultureId)
    {
        ControllerId = controllerId;
        KingdomName = kingdomName;
        CultureId = cultureId;
    }
}
