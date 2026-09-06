using System;
using Common.Commands;
using System.Collections.Generic;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Template.Commands;

/// <summary>
/// TODO fill me out
/// </summary>
internal class TemplateCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    /// <summary>
    /// TODO fill me out
    /// </summary>
    public sealed class TemplateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug";

        public string Name => "template";

        public string Description => "Runs the template debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            return Succeeded("This is a template command");
        }
    }
}
