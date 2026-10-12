using Common.Messaging;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Core;

namespace GameInterface.Services.Ships.Messages;

/// <summary>
/// Raised on a client when it tries to equip a piece on a server-managed ship.
/// </summary>
internal readonly struct ShipUpgradePieceEquipRequested : IEvent
{
    public readonly Ship Ship;
    public readonly string SlotTag;
    public readonly ShipUpgradePiece Piece;

    public ShipUpgradePieceEquipRequested(Ship ship, string slotTag, ShipUpgradePiece piece)
    {
        Ship = ship;
        SlotTag = slotTag;
        Piece = piece;
    }
}
