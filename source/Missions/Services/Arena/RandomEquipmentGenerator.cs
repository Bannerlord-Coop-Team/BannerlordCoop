using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using ItemTypeEnum = TaleWorlds.Core.ItemObject.ItemTypeEnum;

namespace Missions.Services.Arena
{
    /// <summary>
    /// Generator for random fighting equipment
    /// </summary>
    public interface IRandomEquipmentGenerator
    {
        /// <summary>
        /// Creates a new set of random equipment
        /// </summary>
        /// <param name="noHorse">Does the player have a horse</param>
        /// <returns>New set of random equipment</returns>
        Equipment CreateRandomEquipment(bool noHorse);
    }

    /// <inheritdoc cref="IRandomEquipmentGenerator"/>
    public class RandomEquipmentGenerator : IRandomEquipmentGenerator
    {
        //Here im harcoding the elements that are causing the arrows not to spawn and the same for the throwing weapons
        private static readonly HashSet<string> ExcludedItems = new HashSet<string>{"ballista_projectile_burning", "ballista_projectile", "throwing_stone", "boulder",
            "pot", "grapeshot_stack", "grapeshot_fire_stack", "grapeshot_projectile", "grapeshot_fire_projectile" };
        private static readonly ItemTypeEnum[] ArmorLoadout = new ItemTypeEnum[] { ItemTypeEnum.HeadArmor, ItemTypeEnum.Cape, ItemTypeEnum.BodyArmor, ItemTypeEnum.HandArmor, ItemTypeEnum.LegArmor };
        private static readonly ItemTypeEnum[] HorseLoadout = new ItemTypeEnum[] { ItemTypeEnum.Horse, ItemTypeEnum.HorseHarness };
        private static readonly ItemTypeEnum[][] WeaponLoadouts = new ItemTypeEnum[][]
        {
            new ItemTypeEnum[] { ItemTypeEnum.TwoHandedWeapon },
            new ItemTypeEnum[] { ItemTypeEnum.Polearm },
            new ItemTypeEnum[] { ItemTypeEnum.OneHandedWeapon, /*ItemTypeEnum.Thrown*/ },
            //new ItemTypeEnum[] { ItemTypeEnum.Bow, ItemTypeEnum.Arrows, ItemTypeEnum.Thrown }, Both comments are for testing without missiles
            new ItemTypeEnum[] { ItemTypeEnum.OneHandedWeapon, ItemTypeEnum.Shield },
        };

        private static IDictionary<ItemTypeEnum, List<ItemObject>> ExistingItems;

        private readonly Random Random = new Random();

        /// <summary>
        /// Creates dictionary containing all items in game and categorized by itemType
        /// </summary>
        /// <returns>A dictionary</returns>
        private static IDictionary<ItemTypeEnum, List<ItemObject>> InitializeItemDictionary()
        {
            if (ExistingItems?.Count > 0) return ExistingItems;

            if (Game.Current?.ObjectManager == null) return new Dictionary<ItemTypeEnum, List<ItemObject>>();

            IEnumerable<ItemObject> allItems = Game.Current.ObjectManager.GetObjectTypeList<ItemObject>();
            IDictionary<ItemTypeEnum, List<ItemObject>> result = new Dictionary<ItemTypeEnum, List<ItemObject>>();
            
            foreach (var item in allItems)
            {

                bool keyExists = result.ContainsKey(item.ItemType);

                if (ExcludedItems.Contains(item.StringId)) continue;
                if (!keyExists)
                {
                    result.Add(item.ItemType, new List<ItemObject>());
                }

                result[item.ItemType].Add(item);
            }
            return result;
        }

        public RandomEquipmentGenerator()
        {
            
        }

        /// <summary>
        /// Creates random equipment for characters
        /// </summary>
        /// <param name="hasHorse">Boolean for whether character has a horse</param>
        /// <returns>Equipment object for the character</returns>
        public Equipment CreateRandomEquipment(bool noHorse)
        {
            ExistingItems = InitializeItemDictionary();

            Equipment equipment = new Equipment();

            GenerateRandomWeaponEquipment(equipment);
            GenerateRandomArmorEquipment(equipment);

            if (noHorse == false)
            {
                GenerateRandomHorseEquipment(equipment);
            }

            return equipment;
        }

        /// <summary>
        /// Selects a random set of items given their item types
        /// </summary>
        /// <param name="itemTypes">Items to select randomly</param>
        /// <returns>New equipment element with random item</returns>
        private EquipmentElement[] SelectRandomLoadout(ItemTypeEnum[] itemTypes)
        {
            ItemTypeEnum[] loadout = itemTypes;

            EquipmentElement[] equipment = new EquipmentElement[loadout.Length];

            for (int i = 0; i < loadout.Length; i++)
            {
                ItemTypeEnum loadoutItem = loadout[i];

                //Some items doesnt exists, they are harcoded at the dictionary
                int randomItemIndex = Random.Next(ExistingItems[loadoutItem].Count);
                equipment[i] = new EquipmentElement(ExistingItems[loadoutItem][randomItemIndex]);
            }
            return equipment;
        }

        /// <summary>
        /// Generates random weapons and places them into equipment
        /// </summary>
        /// <param name="equipment">Equipment to add random weapons to</param>
        private void GenerateRandomWeaponEquipment(Equipment equipment)
        {
            ItemTypeEnum[] weaponTypes = WeaponLoadouts[Random.Next(WeaponLoadouts.Length)];

            EquipmentElement[] weaponLoadout = SelectRandomLoadout(weaponTypes);

            AddWeaponsToEquipment(weaponLoadout, equipment);
        }

        private void AddWeaponsToEquipment(EquipmentElement[] weaponLoadout, Equipment equipment)
        {
            for (int i = 0; i < weaponLoadout.Length; ++i)
            {
                equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)i, weaponLoadout[i]);
            }
        }

        /// <summary>
        /// Generates random armor and places them into equipment
        /// </summary>
        /// <param name="equipment">Equipment to add armor weapons to</param>
        private void GenerateRandomArmorEquipment(Equipment equipment)
        {
            EquipmentElement[] randomArmorLoadout = SelectRandomLoadout(ArmorLoadout);
            AddArmorToEquipment(randomArmorLoadout, equipment);
        }

        private void AddArmorToEquipment(EquipmentElement[] armorLoadout, Equipment equipment)
        {
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Head, armorLoadout[0]);
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Body, armorLoadout[1]);
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Leg, armorLoadout[2]);
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Gloves, armorLoadout[3]);
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Cape, armorLoadout[4]);
        }

        /// <summary>
        /// Generates random horse equipment and places them into equipment
        /// </summary>
        /// <param name="equipment">Equipment to add random weapons to</param>
        private void GenerateRandomHorseEquipment(Equipment equipment)
        {
            EquipmentElement[] randomHorseLoadout = SelectRandomLoadout(HorseLoadout);
            AddHorseToEquipment(randomHorseLoadout, equipment);
        }

        private void AddHorseToEquipment(EquipmentElement[] horseLoadout, Equipment equipment)
        {
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Horse, horseLoadout[0]);
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.HorseHarness, horseLoadout[1]);
        }
    }
}
