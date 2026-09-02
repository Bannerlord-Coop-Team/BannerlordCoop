using System;
using Common.Commands;
using GameInterface.Configuration;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.CampaignService.Commands;

internal class ModOptionsCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    public sealed class ModConfigListCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mod_config";

        public string Name => "list";

        public string Description => "Reports list.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            StringBuilder stringBuilder = new();

            var modOptions = ModConfigProvider.ModOptions;

            foreach (PropertyInfo property in typeof(ModOptions).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                string name = property.Name;
                string value = property.GetValue(modOptions)?.ToString() ?? "null";

                stringBuilder.AppendLine($"{name}: {value}");
            }

            return Succeeded(stringBuilder.ToString());
        }
    }
}
