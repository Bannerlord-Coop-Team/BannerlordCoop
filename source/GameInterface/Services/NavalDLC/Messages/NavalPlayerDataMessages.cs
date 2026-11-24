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

/// <summary>[Client] The joined save's stashes hold every clan's moored ships, so this is published only on join, unlike <see cref="InitializeClientNavalData"/>.</summary>
public record InitializeClientMooredShips : IEvent
{
    public NavalPlayerData NavalPlayerData { get; }

    public InitializeClientMooredShips(NavalPlayerData navalPlayerData)
    {
        NavalPlayerData = navalPlayerData;
    }
}
