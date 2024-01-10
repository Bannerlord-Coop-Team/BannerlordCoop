using System.Collections.Generic;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Template.Commands;

/// <summary>
/// TODO fill me out
/// </summary>
internal class TemplateCommands
{
    /// <summary>
    /// TODO fill me out
    /// </summary>
    [CommandLineArgumentFunction("template", "coop.debug")]
    public static string TemplateCommand(List<string> strings)
    {
        return "This is a template command";
    }
}
