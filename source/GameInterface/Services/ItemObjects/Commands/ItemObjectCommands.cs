using Autofac;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.ItemObjects.Commands
{
    internal class ItemObjectCommands
    {
        private static bool TryGetObjectManager(out IObjectManager objectManager)
        {
            objectManager = null;
            if (ContainerProvider.TryGetContainer(out var container) == false) return false;

            return container.TryResolve(out objectManager);
        }

        /// <summary>
        /// View select properties of an item object retrieved by string id
        /// </summary>
        [CommandLineArgumentFunction("data", "coop.debug.itemobject")]
        public static string ViewCraftedItemData(List<string> strings)
        {
            if (strings.Count == 0)
            {
                return "Hero name argument required.";
            }

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return "Unable to resolve ObjectManager.";
            }

            string itemId = strings[0];

            StringBuilder stringBuilder = new StringBuilder();

            if (!objectManager.TryGetObject(itemId, out ItemObject itemObject))
            {
                return "Failed to retrieve object for ItemObject id: " + itemId;
            }

            // Add properties as necessary
            stringBuilder.AppendLine(itemObject.StringId + ": " + itemObject.Name);
            stringBuilder.AppendLine("Value: " + itemObject.Value);
            stringBuilder.AppendLine("Difficulty: " + itemObject.Difficulty);
            stringBuilder.AppendLine("Tier: " + itemObject.Tier);
            stringBuilder.AppendLine("Tierf: " + itemObject.Tierf);
            stringBuilder.AppendLine("Appearance: " + itemObject.Appearance);

            string result = stringBuilder.ToString();
            if (result.Length > 0)
            {
                return result;
            }
            return "Item object not found.";
        }
    }
}
