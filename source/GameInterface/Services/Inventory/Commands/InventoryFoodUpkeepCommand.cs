#if DEBUG
using Common;
using Common.Commands;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Roster;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Commands;

/// <summary>Exercises food upkeep while the real inventory screen is open.</summary>
public sealed class InventoryFoodUpkeepCommand : ICoopCommand
{
    private readonly Dictionary<string, ItemRoster> originalRosters = new();

    public string Prefix => "coop.debug.inventory";
    public string Name => "food_upkeep";
    public string Description => "Read inventory food, consume food on the server, or open/close the client inventory.";
    public CoopCommandSide Side => CoopCommandSide.Both;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("action", "read, setup, restore, stage_edits, remove_grain, tick, open, cancel, or done", isRequired: true),
        new ExpectedArgs("party_id", "Registered player party id; required on the server.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objects)) return Failed("Object manager unavailable.");
        MobileParty party;
        if (args.Count > 1)
        {
            if (!objects.TryGetObject<MobileParty>(args[1], out party)) return Failed("Player party not found.");
        }
        else
        {
            if (ModInformation.IsServer) return Failed("Supply the registered client party id on the server.");
            party = MobileParty.MainParty;
        }
        if (party == null || !party.IsPlayerParty()) return Failed("A player party is required.");
        if (!objects.TryGetId(party, out var partyId)) return Failed("Player party is unregistered.");

        var action = args[0].ToLowerInvariant();
        int before = party.ItemRoster.TotalFood;
        if (action == "setup" || action == "restore" || action == "remove_grain")
        {
            if (ModInformation.IsClient) return Failed("Fixture roster changes require the server.");
            if (!objects.TryGetObject<ItemObject>("grain", out var grain) ||
                !objects.TryGetObject<ItemObject>("sheep", out var sheep))
                return Failed("Fixture grain or sheep is unregistered.");
            if (action == "setup")
            {
                if (originalRosters.ContainsKey(partyId)) return Failed("Fixture is already staged.");
                originalRosters.Add(partyId, new ItemRoster(party.ItemRoster));
                party.ItemRoster.Clear();
                party.ItemRoster.AddToCounts(grain, 20);
                party.ItemRoster.AddToCounts(sheep, 2);
            }
            else if (action == "restore")
            {
                if (!originalRosters.TryGetValue(partyId, out var original)) return Failed("Fixture snapshot unavailable.");
                party.ItemRoster.Clear();
                party.ItemRoster.Add(original.ToArray());
                originalRosters.Remove(partyId);
            }
            else
            {
                if (!originalRosters.ContainsKey(partyId)) return Failed("Fixture is not staged.");
                int count = party.ItemRoster.GetItemNumber(grain);
                if (count <= 0) return Failed("No authoritative grain to remove.");
                party.ItemRoster.AddToCounts(grain, -count);
            }
        }
        else if (action == "tick")
        {
            if (ModInformation.IsClient) return Failed("Food consumption must originate on the server.");
            var behavior = Campaign.Current?.GetCampaignBehavior<FoodConsumptionBehavior>();
            if (behavior == null || before <= 0 || party.FoodChange >= 0)
                return Failed("The party must have food and a negative daily food change.");
            int ticks = 0;
            while (party.ItemRoster.TotalFood == before && ticks++ < 32)
                behavior.PartyConsumeFood(party, false);
            if (party.ItemRoster.TotalFood >= before) return Failed("The production food action did not consume food.");
        }
        else if (action != "read")
        {
            if (ModInformation.IsServer || party != MobileParty.MainParty)
                return Failed("Inventory actions require the owning client.");
            var active = (GameStateManager.Current?.ActiveState as InventoryState);
            if (action == "open")
            {
                if (active != null) return Failed("Inventory is already open.");
                InventoryScreenHelper.OpenScreenAsInventory();
            }
            else if (action == "stage_edits")
            {
                if (active == null) return Failed("Inventory is not open.");
                var inventory = active.InventoryLogic;
                var weapon = Hero.MainHero.BattleEquipment[EquipmentIndex.Weapon0];
                var grain = inventory._rosters[1].FirstOrDefault(item => item.EquipmentElement.Item.StringId == "grain");
                var sheep = inventory._rosters[1].FirstOrDefault(item => item.EquipmentElement.Item.StringId == "sheep");
                if (weapon.Item == null || grain.Amount <= 0 ||
                    !inventory.CanSlaughterItem(sheep, InventoryLogic.InventorySide.PlayerInventory))
                    return Failed("Conflict fixture needs an equipped weapon, grain and slaughterable sheep.");
                inventory.AddTransferCommand(TransferCommand.Transfer(1,
                    InventoryLogic.InventorySide.BattleEquipment, InventoryLogic.InventorySide.PlayerInventory,
                    new ItemRosterElement(weapon, 1), EquipmentIndex.Weapon0, EquipmentIndex.None, Hero.MainHero.CharacterObject));
                inventory.AddTransferCommand(TransferCommand.Transfer(grain.Amount,
                    InventoryLogic.InventorySide.PlayerInventory, InventoryLogic.InventorySide.OtherInventory,
                    grain, EquipmentIndex.None, EquipmentIndex.None, Hero.MainHero.CharacterObject));
                inventory.SlaughterItem(sheep);
                if (Hero.MainHero.BattleEquipment[EquipmentIndex.Weapon0].Item != null ||
                    inventory._rosters[1].GetItemNumber(grain.EquipmentElement.Item) != 0)
                    return Failed("Production inventory actions did not stage the conflict.");
            }
            else if (action == "cancel" || action == "done")
            {
                if (active == null) return Failed("Inventory is not open.");
                InventoryScreenHelper.CloseScreen(fromCancel: action == "cancel");
            }
            else return Failed("Unknown inventory action.");
        }

        var logic = ModInformation.IsClient && party == MobileParty.MainParty
            ? (GameStateManager.Current?.ActiveState as InventoryState)?.InventoryLogic : null;
        int backupFood = logic?._rostersBackup[1]?.TotalFood ?? -1;
        return new CoopCommandResult(true,
            $"FOOD_UPKEEP action={action} party={partyId} before={before} food={party.ItemRoster.TotalFood} " +
            $"inventoryOpen={logic != null} backupFood={backupFood} dailyChange={party.FoodChange} " +
            $"items={string.Join(",", party.ItemRoster.Select(item => $"{item.EquipmentElement.Item.StringId}:{item.EquipmentElement.ItemModifier?.StringId}:{item.Amount}"))} " +
            $"leftItems={string.Join(",", logic?._rosters[0].Select(item => $"{item.EquipmentElement.Item.StringId}:{item.Amount}") ?? Enumerable.Empty<string>())} " +
            $"weapon0={party.LeaderHero?.BattleEquipment[EquipmentIndex.Weapon0].Item?.StringId ?? "empty"}");
    }

    private static CoopCommandResult Failed(string message) => new(false, message, "food_upkeep_failed");
}
#endif
