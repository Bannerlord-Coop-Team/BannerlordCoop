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
    /// Builds the spawned server's command line from scratch, mirroring deploy/start-server.bat:
    /// /singleplayer /server, the active module list, then the save + owner pid. Built fresh
    /// (not carried from this process's arguments) because a Steam launch hosts the engine inside
    /// the launcher, so the running process's command line has neither the module list nor the mode.
    /// </summary>
    public static string BuildManagedServerArguments(IReadOnlyList<string> moduleIds, string saveName, int ownerProcessId)
    {
        if (saveName == null) throw new ArgumentNullException(nameof(saveName));
        if (moduleIds == null) throw new ArgumentNullException(nameof(moduleIds));

        var tokens = new List<string>
        {
            "/singleplayer",
            "/server",
            BuildModuleList(moduleIds),
            SaveArgument,
            saveName,
            OwnerArgument,
            ownerProcessId.ToString(CultureInfo.InvariantCulture),
        };

        return string.Join(" ", tokens.Select(QuoteArgument));
    }

    /// <summary>Formats the module ids as the engine's <c>_MODULES_*A*B*_MODULES_</c> launch token.</summary>
    public static string BuildModuleList(IReadOnlyList<string> moduleIds)
    {
        return "_MODULES_" + string.Concat(moduleIds.Select(id => "*" + id)) + "*_MODULES_";
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
