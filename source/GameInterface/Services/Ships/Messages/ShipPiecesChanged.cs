using Common.Messaging;
using TaleWorlds.CampaignSystem.Naval;

namespace GameInterface.Services.Ships.Messages;

internal readonly struct ShipPiecesChanged : IEvent
{
    public readonly Ship Ship;

    public ShipPiecesChanged(Ship ship)
    {
        Ship = ship;
    }
}
