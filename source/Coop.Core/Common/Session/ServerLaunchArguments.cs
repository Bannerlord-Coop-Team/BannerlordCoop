using Common.Network;
using Common.Network.Session;
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
    public const string PasswordArgument = "/cooppassword";
    public const string VisibilityArgument = "/coopvisibility";

    /// <summary>
    /// Builds a fresh server command line with mode, active modules, save, owner PID, and optional
    /// password because a Steam-launched client command line lacks the engine module token.
    /// </summary>
    public static string BuildManagedServerArguments(IReadOnlyList<string> moduleIds, string saveName,
        int ownerProcessId)
        => BuildManagedServerArguments(moduleIds, saveName, ownerProcessId, null, ServerVisibility.Public);

    public static string BuildManagedServerArguments(IReadOnlyList<string> moduleIds, string saveName,
        int ownerProcessId, string password)
        => BuildManagedServerArguments(moduleIds, saveName, ownerProcessId, password, ServerVisibility.Public);

    public static string BuildManagedServerArguments(IReadOnlyList<string> moduleIds, string saveName,
        int ownerProcessId, string password, ServerVisibility visibility)
    {
        if (saveName == null) throw new ArgumentNullException(nameof(saveName));
        if (moduleIds == null) throw new ArgumentNullException(nameof(moduleIds));
        if (!Enum.IsDefined(typeof(ServerVisibility), visibility))
            throw new ArgumentOutOfRangeException(nameof(visibility));

        var tokens = new List<string>
        {
            "/singleplayer",
            "/server",
            BuildModuleList(moduleIds),
            SaveArgument,
            saveName,
            OwnerArgument,
            ownerProcessId.ToString(CultureInfo.InvariantCulture),
            VisibilityArgument,
            FormatVisibility(visibility),
        };

        if (!string.IsNullOrEmpty(password))
        {
            tokens.Add(PasswordArgument);
            tokens.Add(password);
        }

        return string.Join(" ", tokens.Select(QuoteArgument));
    }

    /// <summary>Formats the module ids as the engine's <c>_MODULES_*A*B*_MODULES_</c> launch token.</summary>
    public static string BuildModuleList(IReadOnlyList<string> moduleIds)
    {
        return "_MODULES_" + string.Concat(moduleIds.Select(id => "*" + id)) + "*_MODULES_";
    }

    public static bool TryParse(IReadOnlyList<string> args, out string saveName, out int ownerProcessId)
    {
        return TryParse(args, out saveName, out ownerProcessId, out _);
    }

    public static bool TryParse(IReadOnlyList<string> args, out string saveName, out int ownerProcessId,
        out string password)
        => TryParse(args, out saveName, out ownerProcessId, out password, out _);

    public static bool TryParse(IReadOnlyList<string> args, out string saveName, out int ownerProcessId,
        out string password, out ServerVisibility visibility)
    {
        saveName = null;
        ownerProcessId = 0;
        password = string.Empty;
        visibility = ServerVisibility.Public;
        bool visibilityValid = true;

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
            else if (IsToken(args[i], PasswordArgument) && i + 1 < args.Count)
            {
                password = args[++i];
            }
            else if (IsToken(args[i], VisibilityArgument))
            {
                if (i + 1 >= args.Count || !TryParseVisibility(args[++i], out visibility))
                {
                    // An explicit malformed value must not silently advertise the server publicly.
                    visibility = ServerVisibility.None;
                    visibilityValid = false;
                }
            }
        }

        if (!ConnectionPassword.IsValid(password))
        {
            password = string.Empty;
            return false;
        }

        return saveName != null && visibilityValid;
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

    private static string FormatVisibility(ServerVisibility visibility) => visibility switch
    {
        ServerVisibility.Public => "public",
        ServerVisibility.FriendsOnly => "friends_only",
        ServerVisibility.None => "none",
        _ => throw new ArgumentOutOfRangeException(nameof(visibility)),
    };

    private static bool TryParseVisibility(string value, out ServerVisibility visibility)
    {
        if (string.Equals(value, "public", StringComparison.OrdinalIgnoreCase))
        {
            visibility = ServerVisibility.Public;
            return true;
        }

        if (string.Equals(value, "friends_only", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "friends", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "friendsonly", StringComparison.OrdinalIgnoreCase))
        {
            visibility = ServerVisibility.FriendsOnly;
            return true;
        }

        if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
        {
            visibility = ServerVisibility.None;
            return true;
        }

        // Unknown explicit values fail closed instead of accidentally advertising publicly.
        visibility = ServerVisibility.None;
        return false;
    }
}
