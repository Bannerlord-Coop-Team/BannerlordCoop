using System;
using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.CampaignService.Messages;
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

    public sealed class VoiceEnabledCoopCommand : ICoopCommand
    {
        private readonly INetwork network;
        private readonly IMessageBroker broker;
        public VoiceEnabledCoopCommand(INetwork network, IMessageBroker broker)
        {
            this.network = network;
            this.broker = broker;
        }
        public string Prefix => "coop.debug.mod_config";
        public string Name => "voice_enabled";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public string Description => "Enables or disables voice for the entire server this session.";
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[] { new ExpectedArgs("value", "true or false", isRequired: true) };
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
            {
                if (args.Count != 1 || !bool.TryParse(args[0], out bool enabled))
                    return Failed("Usage: coop.debug.mod_config.voice_enabled true|false");
                ModConfigProvider.ModOptions = new ModOptions(ModConfigProvider.ModOptions, enabled);
                broker.Publish(this, new ModConfigApplied(ModConfigProvider.ModOptions));
                network.SendAll(new NetworkLoadModConfig(ModConfigProvider.ModOptions));
                return Succeeded("Server voice " + (enabled ? "enabled" : "disabled") + " for this session.");
            }
            return Failed("Run this command on the server.");
        }
    }

    public sealed class ModConfigListCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mod_config";

        public string Name => "list";

        public string Description => "Reports list.";

        public CoopCommandSide Side => CoopCommandSide.Both;

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
