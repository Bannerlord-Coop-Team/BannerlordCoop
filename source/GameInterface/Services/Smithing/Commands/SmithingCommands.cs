using System;
using Common.Commands;
using Autofac;
using Common;
using Common.Logging;
using GameInterface.Configuration;
using GameInterface.CoopSessionData;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Smithing.Commands;

internal class SmithingCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    private static readonly ILogger Logger = LogManager.GetLogger<SmithingCommands>();

    /// <summary>
    /// Attempts to get the ObjectManager
    /// </summary>
    private static bool TryGetObjectManager(out IObjectManager objectManager)
    {
        objectManager = null;
        if (ContainerProvider.TryGetContainer(out var container) == false) return false;

        return container.TryResolve(out objectManager);
    }

    /// <summary>
    /// Give debug crafting materials to heroes with a given name
    /// </summary>
    public sealed class CraftingGiveSuppliesCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.crafting";

        public string Name => "give_supplies";

        public string Description => "Runs the give supplies debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name", "The exact hero display name.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager.");
            }

            StringBuilder stringBuilder = new StringBuilder();
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero.Name.ToString() == strings[0])
                {
                    var itemsToAdd = new Dictionary<string, int>()
                    {
                        { "hardwood", 100 },
                        { "iron", 50 },
                        { "charcoal", 100 },
                        { "ironIngot1", 50 },
                        { "ironIngot2", 50 },
                        { "ironIngot3", 50 },
                        { "ironIngot4", 50 },
                        { "ironIngot5", 50 },
                        { "ironIngot6", 50 },
                        { "empire_sword_4_t4", 3 }
                    };

                    foreach (var itemId in itemsToAdd.Keys)
                    {
                        if (!objectManager.TryGetObject(itemId, out ItemObject itemObject))
                        {
                            stringBuilder.AppendLine("Failed to retrieve object for ItemObject id: " + itemId);
                        }
                        else
                        {
                            hero.PartyBelongedTo.ItemRoster.AddToCounts(itemObject, itemsToAdd[itemId]);
                        }
                    }

                    stringBuilder.AppendLine(strings[0] + " was given smithing supplies.");
                }
            }

            string result = stringBuilder.ToString();
            if (result.Length > 0)
            {
                return Succeeded(result);
            }
            return Failed("Hero not found.");
        }
    }

    /// <summary>
    /// Unlock all crafting pieces on a client
    /// OpenPart is patched to already update CoopSession and persist across sessions
    /// </summary>
    public sealed class CraftingUnlockAllCraftingPiecesCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.crafting";

        public string Name => "unlock_all_crafting_pieces";

        public string Description => "Runs the unlock all crafting pieces debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (ModInformation.IsServer)
                return Failed("Command can only be run on a client.");

            if (!ModConfigProvider.ModOptions.ClientsCanUseCheats)
                return Failed("Cheats are currently disabled on clients. Enable in mod-config.");

            var craftingCampaignBehavior = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>();
            if (craftingCampaignBehavior == null)
                return Failed("Unable to get crafting campaign behavior.");

            foreach (var craftingTemplate in CraftingTemplate.All)
            {
                foreach (var craftingPiece in craftingTemplate.Pieces)
                {
                    // Turn off notification, otherwise unlocking client gets hundreds of notifications
                    craftingCampaignBehavior.OpenPart(craftingPiece, craftingTemplate, false);
                }
            }

            return Succeeded("All crafting pieces unlocked.");
        }
    }

    /// <summary>
    /// View town orders for a specified town
    /// </summary>
    public sealed class CraftingTownOrdersCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.crafting";

        public string Name => "town_orders";

        public string Description => "Runs the town orders debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("town_name", "The exact town display name.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {

            if (TryGetObjectManager(out var objectManager) == false) return Failed("Unable to resolve ObjectManager.");

            StringBuilder stringBuilder = new StringBuilder();
            foreach (var town in Town.AllTowns)
            {
                if (town.Name.ToString() == strings[0])
                {
                    stringBuilder.AppendLine("Target town " + town.Name.ToString() + " has orders:");
                    CraftingOrder[] slots = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>()._craftingOrders[town].Slots;
                    foreach (CraftingOrder order in slots)
                    {
                        stringBuilder.AppendLine("Order slot: " + order?.OrderDifficulty + " for hero: " + order?.OrderOwner);
                    }
                }
            }

            string result = stringBuilder.ToString();
            if (result.Length > 0)
            {
                return Succeeded(result);
            }
            return Failed("Town not found.");
        }
    }

    /// <summary>
    /// Add orders to a town by a hero in that town
    /// </summary>
    public sealed class CraftingAddTownOrderCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.crafting";

        public string Name => "add_town_order";

        public string Description => "Runs the add town order debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name", "The exact hero display name.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");

            var heroName = strings[0]; // Example hero name: "Vaminesa the Minter"

            StringBuilder stringBuilder = new StringBuilder();
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero.Name.ToString() == heroName)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>().CreateTownOrder(hero, i);
                    }

                    stringBuilder.AppendLine("Orders have been added for " + heroName + " in " + hero.CurrentSettlement.Town.Name.ToString());
                }
            }

            string result = stringBuilder.ToString();
            if (result.Length > 0)
            {
                return Succeeded(result);
            }
            return Failed("Town not found.");
        }
    }

    /// <summary>
    /// Add all existing crafted items to a given hero
    /// </summary>
    public sealed class CraftingAddCraftedItemsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.crafting";

        public string Name => "add_crafted_items";

        public string Description => "Runs the add crafted items debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name", "The exact hero display name.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");

            if (TryGetObjectManager(out var objectManager) == false) return Failed("Unable to resolve ObjectManager.");

            var craftedItemPrefix = "crafted_item_";

            StringBuilder stringBuilder = new StringBuilder();
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero.Name.ToString() == strings[0])
                {
                    int craftedItemCount = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>()._craftedItemCount;
                    for (int i = 0; i < craftedItemCount; i++)
                    {
                        string craftedItemId = craftedItemPrefix + i.ToString();
                        if (!objectManager.TryGetObject(craftedItemId, out ItemObject itemObject))
                        {
                            stringBuilder.AppendLine("Failed to retrieve object for ItemObject id: " + craftedItemId);
                        }
                        else
                        {
                            hero.PartyBelongedTo.ItemRoster.AddToCounts(itemObject, 1);
                        }
                    }

                    stringBuilder.AppendLine(strings[0] + " was given all crafted items.");
                }
            }

            string result = stringBuilder.ToString();
            if (result.Length > 0)
            {
                return Succeeded(result);
            }
            return Failed("Town not found.");
        }
    }

    /// <summary>
    /// View crafting stamina of all heroes in party on client and all heroes on server
    /// </summary>
    public sealed class CraftingStaminaCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.crafting";

        public string Name => "stamina";

        public string Description => "Runs the stamina debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            StringBuilder stringBuilder = new StringBuilder();
            CraftingCampaignBehavior craftingCampaignBehavior = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>();

            foreach (var heroCraftingRecord in craftingCampaignBehavior._heroCraftingRecords)
            {
                if (ModInformation.IsServer || heroCraftingRecord.Key.PartyBelongedTo == Hero.MainHero.PartyBelongedTo)
                {
                    stringBuilder.AppendLine($"{heroCraftingRecord.Key.Name} ({heroCraftingRecord.Key.StringId}): {heroCraftingRecord.Value.CraftingStamina}");
                }
            }

            string result = stringBuilder.ToString();
            if (result.Length > 0)
            {
                return Succeeded(result);
            }
            return Failed("No hero crafting stamina data was found.");
        }
    }

    /// <summary>
    /// View crafted item history, showing all players on server and current player on client
    /// </summary>
    public sealed class CraftingCraftedItemHistoryCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.crafting";

        public string Name => "crafted_item_history";

        public string Description => "Runs the crafted item history debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var coopSessionProvider)) return Failed("Unable to resolve CoopSessionProvider");

            StringBuilder stringBuilder = new StringBuilder();
            if (ModInformation.IsServer)
            {
                foreach (KeyValuePair<string, List<string>> craftedItemHistory in coopSessionProvider.CoopSession.CraftingPlayerData.PlayerCraftedItemsHistory)
                {
                    stringBuilder.AppendLine(craftedItemHistory.Key);
                    foreach (string craftedItemId in craftedItemHistory.Value)
                    {
                        stringBuilder.AppendLine(craftedItemId);
                    }
                }
            }
            else
            {
                CraftingCampaignBehavior craftingCampaignBehavior = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>();
                foreach (ItemObject item in craftingCampaignBehavior._cratingItemsHistory)
                {
                    stringBuilder.AppendLine(item.StringId);
                }
            }

            string result = stringBuilder.ToString();
            if (result.Length > 0)
            {
                return Succeeded(result);
            }
            return Failed("Error finding crafting player data or no crafted item history");
        }
    }

    /// <summary>
    /// View crafted pieces xp, showing all players on server and current player on client
    /// </summary>
    public sealed class CraftingCraftingPiecesXpCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.crafting";

        public string Name => "crafting_pieces_xp";

        public string Description => "Runs the crafting pieces xp debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var coopSessionProvider)) return Failed("Unable to resolve CoopSessionProvider");

            StringBuilder stringBuilder = new StringBuilder();
            if (ModInformation.IsServer)
            {
                foreach (KeyValuePair<string, Dictionary<string, float>> playerPartXp in coopSessionProvider.CoopSession.CraftingPlayerData.PlayerOpenNewPartXpDictionary)
                {
                    stringBuilder.AppendLine(playerPartXp.Key);
                    foreach (KeyValuePair<string, float> partXp in playerPartXp.Value)
                    {
                        stringBuilder.AppendLine(partXp.Key + ": " + partXp.Value);
                    }
                }
            }
            else
            {
                CraftingCampaignBehavior craftingCampaignBehavior = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>();
                foreach (KeyValuePair<CraftingTemplate, float> partXp in craftingCampaignBehavior._openNewPartXpDictionary)
                {
                    stringBuilder.AppendLine(partXp.Key + ": " + partXp.Value);
                }
            }

            string result = stringBuilder.ToString();
            if (result.Length > 0)
            {
                return Succeeded(result);
            }
            return Failed("Error finding crafting player data or no parts xp data");
        }
    }

    /// <summary>
    /// View unlocked crafted pieces, showing all players on server and current player on client
    /// </summary>
    public sealed class CraftingUnlockedCraftingPiecesCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.crafting";

        public string Name => "unlocked_crafting_pieces";

        public string Description => "Runs the unlocked crafting pieces debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var coopSessionProvider)) return Failed("Unable to resolve CoopSessionProvider");

            StringBuilder stringBuilder = new StringBuilder();
            if (ModInformation.IsServer)
            {
                foreach (KeyValuePair<string, Dictionary<string, List<string>>> playerUnlockedPieces in coopSessionProvider.CoopSession.CraftingPlayerData.PlayerOpenedPartsDictionary)
                {
                    stringBuilder.AppendLine(playerUnlockedPieces.Key);
                    foreach (KeyValuePair<string, List<string>> templateUnlockedPieces in playerUnlockedPieces.Value)
                    {
                        stringBuilder.AppendLine(templateUnlockedPieces.Key + ": " + templateUnlockedPieces.Value.Count);
                    }
                }
            }
            else
            {
                CraftingCampaignBehavior craftingCampaignBehavior = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>();
                foreach (KeyValuePair<CraftingTemplate, List<CraftingPiece>> templateUnlockedPieces in craftingCampaignBehavior._openedPartsDictionary)
                {
                    stringBuilder.AppendLine(templateUnlockedPieces.Key + ": " + templateUnlockedPieces.Value.Count);
                }
            }

            string result = stringBuilder.ToString();
            if (result.Length > 0)
            {
                return Succeeded(result);
            }
            return Failed("Error finding crafting player data or no unlocked parts");
        }
    }
}
