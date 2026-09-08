namespace TaleWorlds.Library;

// Supplies the vanilla registry seam without loading Bannerlord or native DLLs.
public static class CommandLineFunctionality
{
    private static readonly Dictionary<string, object> AllFunctions = new()
    {
        ["coop.debug.legacy"] = new(), ["campaign.do_something"] = new(), ["coop.unregistered"] = new(),
    };
    public static void CollectCommandLineFunctions() { }
    public static string CallFunction(string command, List<string> arguments, out bool found)
    {
        if (!command.StartsWith("coop.debug.", StringComparison.Ordinal))
            throw new InvalidOperationException("Arbitrary vanilla dispatch is forbidden in this test.");
        found = AllFunctions.ContainsKey(command);
        return "legacy result";
    }
}
