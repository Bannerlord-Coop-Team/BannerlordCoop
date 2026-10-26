#if DEBUG
using Common;
using Common.Commands;
using System;
using System.Linq;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Commands;

public sealed class InventoryOpenCommand : ICoopCommand
{
    public string Prefix => "coop.debug.inventory";
    public string Name => "open";
    public string Description => "Open the owning client's inventory.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (ModInformation.IsServer) return new(false, "Requires the owning client.");
        var active = GameStateManager.Current?.ActiveState as InventoryState;
        if (active != null) return new(false, "Inventory is already open.");
        InventoryScreenHelper.OpenScreenAsInventory();
        return new(true, "Inventory action completed.");
    }
}
#endif
