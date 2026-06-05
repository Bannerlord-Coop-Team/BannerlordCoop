using System.Collections.Generic;

namespace GameInterface.Utils.Commands;

internal sealed class CommandContext
{
    public string CommandName { get; }
    public string Usage { get; }
    public List<string> Args { get; }

    public CommandContext(string commandName, string usage, List<string> args)
    {
        CommandName = commandName;
        Usage = usage;
        Args = args ?? new List<string>();
    }

    public bool RequireServer(out string error)
    {
        return CommandHelpers.IsServerOnlyCommand(out error, CommandName);
    }

    public bool RequireArgCount(int expectedCount, out string error)
    {
        return CommandHelpers.HasArgCount(Args, expectedCount, Usage, out error);
    }

    public bool TryGetArg(int index, string name, out string value, out string error)
    {
        return CommandHelpers.TryGetRequiredArg(Args, index, name, Usage, out value, out error);
    }
}