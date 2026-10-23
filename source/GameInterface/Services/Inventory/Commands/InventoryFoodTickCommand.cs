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

public sealed class InventoryFoodTickCommand : ICoopCommand
{
    public string Prefix => "coop.debug.inventory";
    public string Name => "food_tick";
    public string Description => "Tick player party food.";
    public CoopCommandSide Side => CoopCommandSide.Server;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("party_id", "Registered player party id; required on the server.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objects)) return new(false, "Object manager unavailable.");
        MobileParty party;
        if (args.Count > 0)
        {
            if (!objects.TryGetObject<MobileParty>(args[0], out party)) return new(false, "Player party not found.");
        }
        else
        {
            if (ModInformation.IsServer) return new(false, "Supply the registered client party id on the server.");
            party = MobileParty.MainParty;
        }
        if (party == null || !party.IsPlayerParty()) return new(false, "A player party is required.");
        if (!objects.TryGetId(party, out var partyId)) return new(false, "Player party is unregistered.");
        int before = party.ItemRoster.TotalFood;
        if (ModInformation.IsClient) return new CoopCommandResult(false, "Food consumption must originate on the server.");
        var behavior = Campaign.Current?.GetCampaignBehavior<FoodConsumptionBehavior>();
        if (behavior == null || before <= 0 || party.FoodChange >= 0)
            return new CoopCommandResult(false, "The party must have food and a negative daily food change.");
        int ticks = 0;
        while (party.ItemRoster.TotalFood == before && ticks++ < 32)
            behavior.PartyConsumeFood(party, false);
        if (party.ItemRoster.TotalFood >= before) return new CoopCommandResult(false, "The production food action did not consume food.");
        var logic = ModInformation.IsClient && party == MobileParty.MainParty
            ? (GameStateManager.Current?.ActiveState as InventoryState)?.InventoryLogic : null;
        int backupFood = logic?._rostersBackup[1]?.TotalFood ?? -1;
        return new CoopCommandResult(true,
            $"FOOD_UPKEEP action=tick party={partyId} before={before} food={party.ItemRoster.TotalFood} " +
            $"inventoryOpen={logic != null} backupFood={backupFood} dailyChange={party.FoodChange} " +
            $"items={string.Join(",", party.ItemRoster.Select(item => $"{item.EquipmentElement.Item.StringId}:{item.EquipmentElement.ItemModifier?.StringId}:{item.Amount}"))} " +
            $"leftItems={string.Join(",", logic?._rosters[0].Select(item => $"{item.EquipmentElement.Item.StringId}:{item.Amount}") ?? Enumerable.Empty<string>())} " +
            $"weapon0={party.LeaderHero?.BattleEquipment[EquipmentIndex.Weapon0].Item?.StringId ?? "empty"}");
    }
}
#endif
