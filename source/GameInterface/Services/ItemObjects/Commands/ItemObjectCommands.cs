using Common.Commands;
using GameInterface.Utils.Commands;
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

        /// <summary>
        /// View select properties of an item object retrieved by name or id
        /// </summary>
        public sealed class ItemObjectDataCoopCommand : ICoopCommand
        {
            private readonly ICommandObjectResolver resolver;

            public ItemObjectDataCoopCommand(ICommandObjectResolver resolver)
            {
                this.resolver = resolver;
            }

            public string Prefix => "coop.debug.item_object";

            public string Name => "data";

            public string Description => "Reports data.";

            public CoopCommandSide Side => CoopCommandSide.Both;

            public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
            {
                new ExpectedArgs("item_id", "Item name or id; quote names with spaces.", isRequired: true),
            };

            public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
            {
                if (!resolver.TryResolveItemObject(strings[0], out var itemObject, out var error))
                {
                    return Failed(error);
                }

                StringBuilder stringBuilder = new StringBuilder();

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
