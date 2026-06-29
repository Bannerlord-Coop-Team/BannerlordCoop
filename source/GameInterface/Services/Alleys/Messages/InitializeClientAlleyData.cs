using Common.Messaging;

namespace GameInterface.Services.Alleys.Messages;

/// <summary>
/// Carries the server's authoritative <see cref="AlleyPlayerData"/> to a joining client as part of
/// the save transfer, so the client can restore its owned alleys' garrison/overseer once its main
/// hero is set (see AlleyInitializationHandler).
/// </summary>
public record InitializeClientAlleyData : IEvent
{
    public AlleyPlayerData AlleyPlayerData;

    public InitializeClientAlleyData(AlleyPlayerData alleyPlayerData)
    {
        AlleyPlayerData = alleyPlayerData;
    }
}
