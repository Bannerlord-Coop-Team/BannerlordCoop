using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Coop.Core.Common.Session;

/// <summary>
/// Command-line contract between a hosting client and the server process it spawns: the
/// client builds the child's arguments from its own, the child parses them back.
/// </summary>
public static class ServerLaunchArguments
{
    public const string SaveArgument = "/coopsave";
    public const string OwnerArgument = "/coopowner";

    /// <summary>
    /// Rebuilds this process's arguments into the spawned server's: role flags and Steam
    /// join tokens are dropped, /server is forced, and the save + owner pid appended.
    /// Everything else (module list, platform id) is kept so the child sees the same
    /// module set and save directory as the client that spawned it.
    /// </summary>
    public static string BuildChildArguments(IReadOnlyList<string> currentArgs, string saveName, int ownerProcessId)
    {
        if (saveName == null) throw new ArgumentNullException(nameof(saveName));

        var kept = new List<string>();
        for (int i = 0; i < currentArgs.Count; i++)
        {
            var arg = currentArgs[i];

            if (IsToken(arg, "/server") || IsToken(arg, "/client") || IsToken(arg, "/autoconnect")) continue;

            // These carry a value; drop it with them.
            if (IsToken(arg, SaveArgument) || IsToken(arg, OwnerArgument) || IsToken(arg, "+connect_lobby"))
            {
                i++;
                continue;
            }

            kept.Add(arg);
        }

        kept.Add("/server");
        kept.Add(SaveArgument);
        kept.Add(saveName);
        kept.Add(OwnerArgument);
        kept.Add(ownerProcessId.ToString(CultureInfo.InvariantCulture));

        return string.Join(" ", kept.Select(QuoteArgument));
    }

    public static bool TryParse(IReadOnlyList<string> args, out string saveName, out int ownerProcessId)
    {
        saveName = null;
        ownerProcessId = 0;

        for (int i = 0; i < args.Count; i++)
        {
            if (IsToken(args[i], SaveArgument) && i + 1 < args.Count)
            {
                saveName = args[++i];
            }
            else if (IsToken(args[i], OwnerArgument) && i + 1 < args.Count)
            {
                int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out ownerProcessId);
            }
        }

        return saveName != null;
    }

    /// <summary>
    /// Quotes one argument per Windows command-line rules so the child's
    /// Environment.GetCommandLineArgs sees exactly the original value.
    /// </summary>
    public static string QuoteArgument(string arg)
    {
        if (arg.Length > 0 && !arg.Any(c => c == ' ' || c == '\t' || c == '"')) return arg;

        var builder = new StringBuilder("\"");
        int backslashes = 0;
        foreach (var c in arg)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            if (c == '"')
            {
                // Backslashes before a quote must be doubled, plus one to escape the quote itself.
                builder.Append('\\', (backslashes * 2) + 1);
                builder.Append('"');
                backslashes = 0;
                continue;
            }

            builder.Append('\\', backslashes);
            builder.Append(c);
            backslashes = 0;
        }

        // Backslashes before the closing quote must be doubled too.
        builder.Append('\\', backslashes * 2);
        builder.Append('"');
        return builder.ToString();
    }

    private static bool IsToken(string arg, string token) => string.Equals(arg, token, StringComparison.OrdinalIgnoreCase);
}
