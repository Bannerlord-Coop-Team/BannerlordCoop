using Common.Commands;
using Autofac;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.ItemObjects.Commands
{
    internal class ItemObjectCommands
    {
        private static CoopCommandResult Succeeded(string output) =>
            new CoopCommandResult(true, output);

        private static CoopCommandResult Failed(string output) =>
            new CoopCommandResult(false, output, "command_failed");

        private static bool TryGetObjectManager(out IObjectManager objectManager)
        {
            objectManager = null;
            if (ContainerProvider.TryGetContainer(out var container) == false) return false;

            return container.TryResolve(out objectManager);
        }

        /// <summary>
        /// View select properties of an item object retrieved by string id
        /// </summary>
        public sealed class ItemObjectDataCoopCommand : ICoopCommand
        {
            public string Prefix => "coop.debug.item_object";

            public string Name => "data";

            public string Description => "Reports data.";

            public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
            {
                new ExpectedArgs("item_id", "The registered item object id.", isRequired: true),
            };

            public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
            {

                if (TryGetObjectManager(out var objectManager) == false)
                {
                    return Failed("Unable to resolve ObjectManager.");
                }

                string itemId = strings[0];

                StringBuilder stringBuilder = new StringBuilder();

                if (!objectManager.TryGetObject(itemId, out ItemObject itemObject))
                {
                    return Failed("Failed to retrieve object for ItemObject id: " + itemId);
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
                    return Succeeded(result);
                }
                return Failed("Item object not found.");
            }
        }
    }
}
