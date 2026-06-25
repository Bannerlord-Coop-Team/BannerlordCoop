using Common.Messaging;

namespace GameInterface.Services.Kingdoms.Messages;

public record KingdomCreationRequested : IEvent
{
    public string KingdomName { get; }
    public string CultureId { get; }

    public KingdomCreationRequested(string kingdomName, string cultureId)
    {
        KingdomName = kingdomName;
        CultureId = cultureId;
    }
}
