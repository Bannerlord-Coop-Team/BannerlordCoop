using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using System;
using TaleWorlds.CampaignSystem.Inventory;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class PlayerPartyTradeContextTests : IDisposable
{
    public void Dispose()
    {
        PlayerPartyTradeContext.End();
    }

    [Fact]
    public void CanTransfer_AllowsBothSidesWhenInactive()
    {
        Assert.True(PlayerPartyTradeContext.CanTransfer(new TransferCommand
        {
            FromSide = InventoryLogic.InventorySide.PlayerInventory
        }));
        Assert.True(PlayerPartyTradeContext.CanTransfer(new TransferCommand
        {
            FromSide = InventoryLogic.InventorySide.OtherInventory
        }));
    }

    [Fact]
    public void CanTransfer_BlocksOpposingSideWhenActive()
    {
        PlayerPartyTradeContext.Begin("session-1");

        Assert.True(PlayerPartyTradeContext.CanTransfer(new TransferCommand
        {
            FromSide = InventoryLogic.InventorySide.PlayerInventory
        }));
        Assert.False(PlayerPartyTradeContext.CanTransfer(new TransferCommand
        {
            FromSide = InventoryLogic.InventorySide.OtherInventory
        }));
    }

    [Fact]
    public void CanTransfer_BlocksAllTransfersAfterEitherPlayerAccepted()
    {
        PlayerPartyTradeContext.Begin("session-1");
        PlayerPartyTradeContext.UpdateAcceptance(localAccepted: false, remoteAccepted: true);

        Assert.False(PlayerPartyTradeContext.CanTransfer(new TransferCommand
        {
            FromSide = InventoryLogic.InventorySide.PlayerInventory
        }));

        PlayerPartyTradeContext.UpdateAcceptance(localAccepted: true, remoteAccepted: false);

        Assert.False(PlayerPartyTradeContext.CanTransfer(new TransferCommand
        {
            FromSide = InventoryLogic.InventorySide.PlayerInventory
        }));
    }

    [Fact]
    public void CanAccept_BlocksRepeatAcceptAfterLocalAccept()
    {
        PlayerPartyTradeContext.Begin("session-1");

        Assert.True(PlayerPartyTradeContext.CanAccept());

        PlayerPartyTradeContext.UpdateAcceptance(localAccepted: true, remoteAccepted: false);

        Assert.False(PlayerPartyTradeContext.CanAccept());
        Assert.False(PlayerPartyTradeContext.CanCancel());
    }

    [Fact]
    public void CanReset_BlocksResetWhileActive()
    {
        Assert.True(PlayerPartyTradeContext.CanReset());

        PlayerPartyTradeContext.Begin("session-1");

        Assert.False(PlayerPartyTradeContext.CanReset());
    }

    [Fact]
    public void CanOffer_BlocksUnknownBarterableWhenActive()
    {
        Assert.True(PlayerPartyTradeContext.CanOffer(null));

        PlayerPartyTradeContext.Begin("session-1");

        Assert.False(PlayerPartyTradeContext.CanOffer(null));
    }
}
