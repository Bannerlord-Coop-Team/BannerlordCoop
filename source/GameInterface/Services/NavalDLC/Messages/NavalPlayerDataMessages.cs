using Common.Messaging;

namespace GameInterface.Services.NavalDLC.Messages;

public record InitializeClientNavalData : IEvent
{
    public NavalPlayerData NavalPlayerData { get; }

    public InitializeClientNavalData(NavalPlayerData navalPlayerData)
    {
        NavalPlayerData = navalPlayerData;
    }
}
