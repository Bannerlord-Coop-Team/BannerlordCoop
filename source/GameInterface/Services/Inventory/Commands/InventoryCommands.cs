using Common.Commands;
using Autofac;
using Common;
using Common.Logging;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.Commands;
using Serilog;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Inventory.Commands;

internal class InventoryCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    private static readonly ILogger Logger = LogManager.GetLogger<InventoryCommands>();

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
    /// View item ids in player inventories
    /// </summary>
    public sealed class InventoryItemIdsCoopCommand : ICoopCommand
    {
        // Resolves hero arguments by name or id.
        private readonly ICommandObjectResolver resolver;

        public InventoryItemIdsCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.inventory";

        public string Name => "item_ids";

        public string Description => "Runs the item ids debug operation.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name", "Hero name or id; quote names with spaces.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {

            if (!resolver.TryResolveHero(strings[0], out var hero, out var error)) return Failed(error);

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(hero.Name.ToString());
            foreach (var rosterElement in hero.PartyBelongedTo.ItemRoster)
            {
                stringBuilder.AppendLine(rosterElement.EquipmentElement.Item.StringId + ": " + rosterElement._amount);
            }

            return Succeeded(stringBuilder.ToString());
        }
    }

    /// <summary>
    /// View item values in player inventories
    /// </summary>
    public sealed class InventoryItemValuesCoopCommand : ICoopCommand
    {
        // Resolves hero arguments by name or id.
        private readonly ICommandObjectResolver resolver;

        public InventoryItemValuesCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.inventory";

        public string Name => "item_values";

        public string Description => "Runs the item values debug operation.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name", "Hero name or id; quote names with spaces.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {

            if (!resolver.TryResolveHero(strings[0], out var hero, out var error)) return Failed(error);

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(hero.Name.ToString());
            foreach (var rosterElement in hero.PartyBelongedTo.ItemRoster)
            {
                stringBuilder.AppendLine(rosterElement.EquipmentElement.Item.StringId + ": " + rosterElement.EquipmentElement.Item.Value + "(" + rosterElement.EquipmentElement.ItemValue + ")");
            }

            return Succeeded(stringBuilder.ToString());
        }
    }

    /// <summary>
    /// Output equipment ids of battle, civilian and stealth equipment for a specific hero
    /// </summary>
    public sealed class InventoryHeroEquipmentCoopCommand : ICoopCommand
    {
        // Resolves hero arguments by name or id.
        private readonly ICommandObjectResolver resolver;

        public InventoryHeroEquipmentCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.inventory";

        public string Name => "hero_equipment";

        public string Description => "Runs the hero equipment debug operation.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name", "Hero name or id; quote names with spaces.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {

            if (!resolver.TryResolveHero(strings[0], out var hero, out var error)) return Failed(error);

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(hero.Name.ToString());

            stringBuilder.AppendLine("BattleEquipment");
            foreach (var equipmentElement in hero.BattleEquipment._itemSlots) stringBuilder.AppendLine(equipmentElement.Item?.StringId ?? "[EMPTY]");

            stringBuilder.AppendLine("CivilianEquipment");
            foreach (var equipmentElement in hero.CivilianEquipment._itemSlots) stringBuilder.AppendLine(equipmentElement.Item?.StringId ?? "[EMPTY]");

            stringBuilder.AppendLine("StealthEquipment");
            foreach (var equipmentElement in hero.StealthEquipment._itemSlots) stringBuilder.AppendLine(equipmentElement.Item?.StringId ?? "[EMPTY]");

            return Succeeded(stringBuilder.ToString());
        }
    }

    /// <summary>
    /// Give debug animals to a hero with a given name
    /// </summary>
    public sealed class InventoryGiveAnimalsCoopCommand : ICoopCommand
    {
        // Resolves hero arguments by name or id.
        private readonly ICommandObjectResolver resolver;

        public InventoryGiveAnimalsCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.inventory";

        public string Name => "give_animals";

        public string Description => "Runs the give animals debug operation.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name", "Hero name or id; quote names with spaces.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager.");
            }

            if (!resolver.TryResolveHero(strings[0], out var hero, out var error)) return Failed(error);

            StringBuilder stringBuilder = new StringBuilder();
            var itemsToAdd = new Dictionary<string, int>()
            {
                { "sheep", 5 },
                { "cow", 5 },
                { "hog", 5 },
                { "goose", 5 },
                { "chicken", 5 }
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

            stringBuilder.AppendLine(strings[0] + " was given animals.");

            return Succeeded(stringBuilder.ToString());
        }
    }

    /// <summary>
    /// Give war horses to a hero with a given name
    /// </summary>
    public sealed class InventoryGiveWarhorsesCoopCommand : ICoopCommand
    {
        // Resolves hero arguments by name or id.
        private readonly ICommandObjectResolver resolver;

        public InventoryGiveWarhorsesCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.inventory";

        public string Name => "give_warhorses";

        public string Description => "Runs the give warhorses debug operation.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name", "Hero name or id; quote names with spaces.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");

            if (TryGetObjectManager(out var objectManager) == false) return Failed("Unable to resolve ObjectManager.");

            if (!resolver.TryResolveHero(strings[0], out var hero, out var error)) return Failed(error);

            StringBuilder stringBuilder = new StringBuilder();
            var itemsToAdd = new Dictionary<string, int>()
            {
                { "t2_empire_horse", 3 },
                { "t2_khuzait_horse", 2 }
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

            stringBuilder.AppendLine(strings[0] + " was given war horses.");

            return Succeeded(stringBuilder.ToString());
        }
    }
}
