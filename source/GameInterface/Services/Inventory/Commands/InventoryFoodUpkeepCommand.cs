#if DEBUG
using Common;
using Common.Commands;
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
    public string Prefix => "coop.debug.inventory";
    public string Name => "food_upkeep";
    public string Description => "Read inventory food, consume food on the server, or open/close the client inventory.";
    public CoopCommandSide Side => CoopCommandSide.Both;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("action", "read, tick, open, cancel, or done", isRequired: true),
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
        if (action == "tick")
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
            else if (action == "cancel" || action == "done")
            {
                if (active == null) return Failed("Inventory is not open.");
                InventoryScreenHelper.CloseScreen(fromCancel: action == "cancel");
            }
            else return Failed("Unknown inventory action.");
        }

        var logic = (GameStateManager.Current?.ActiveState as InventoryState)?.InventoryLogic;
        int backupFood = logic?._rostersBackup[1]?.TotalFood ?? -1;
        return new CoopCommandResult(true,
            $"FOOD_UPKEEP action={action} party={partyId} before={before} food={party.ItemRoster.TotalFood} " +
            $"inventoryOpen={logic != null} backupFood={backupFood} dailyChange={party.FoodChange}");
    }

    private static CoopCommandResult Failed(string message) => new(false, message, "food_upkeep_failed");
}
#endif
