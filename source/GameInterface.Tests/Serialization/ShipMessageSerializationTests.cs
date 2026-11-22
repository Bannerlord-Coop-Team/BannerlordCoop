using GameInterface.Services.Ships.Data;
using GameInterface.Services.Ships.Messages;
using ProtoBuf;
using System;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class ShipMessageSerializationTests
{
    [Fact]
    public void PiecesChanged_PreservesSlotOrderAndEmptySlots()
    {
        var slots = new[]
        {
            new ShipSlotPieceData("fore", 7),
            new ShipSlotPieceData("aft", 0),
        };

        var copy = Serializer.DeepClone(new NetworkShipPiecesChanged(3, slots, new uint[] { 7 }, 105f));

        Assert.Equal(3u, copy.ShipHandle);
        Assert.Equal(new[] { "fore", "aft" }, copy.Slots.Select(slot => slot.SlotTag));
        Assert.Equal(new uint[] { 7, 0 }, copy.Slots.Select(slot => slot.PieceHandle));
        Assert.True(copy.HasUnlockedPieces);
        Assert.Equal(new uint[] { 7 }, copy.UnlockedPieceHandles);
        Assert.Equal(105f, copy.HitPoints);
    }

    [Fact]
    public void PiecesChanged_DistinguishesNullAndEmptyUnlockedPieces()
    {
        var withNull = Serializer.DeepClone(new NetworkShipPiecesChanged(3, Array.Empty<ShipSlotPieceData>(), null, 0f));
        var withEmpty = Serializer.DeepClone(new NetworkShipPiecesChanged(3, Array.Empty<ShipSlotPieceData>(), Array.Empty<uint>(), 0f));

        Assert.False(withNull.HasUnlockedPieces);
        Assert.True(withEmpty.HasUnlockedPieces);
    }
}
