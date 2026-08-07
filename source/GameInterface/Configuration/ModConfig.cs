using Common.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TaleWorlds.Library;

namespace GameInterface.Configuration;

/// <summary>
/// Loads mod-config.json from the mod's shared CoopData folder — ONE gameplay
/// config however the world is hosted: %COOP_DATA_DIR% when a dedicated server
/// set it (it points at &lt;game user data&gt;\CoopData, or at the server's own data
/// dir for deployments that keep the file there), else %BANNERLORD_USER_DIR%
/// (older DS builds, containers), else &lt;engine user directory&gt;\CoopData — for a
/// client-hosted session that is Documents\Mount and Blade II
/// Bannerlord\CoopData, beside the mod's CoopMapData. Seeds it from the
/// module's template when absent, and falls back to defaults if it cannot be
/// read. Existing files are migrated in place by activating formerly commented
/// settings without replacing active operator values.
/// </summary>
internal sealed class ModConfig : IModConfig
{
    private const string FileName = "mod-config.json";

    /// <summary>The mod's folder under the game's user data; the DS nests its own
    /// DedicatedServer home inside the same folder.</summary>
    private const string CoopDataFolderName = "CoopData";

    /// <summary>Ships in the module root — the only copy of the defaults.</summary>
    private const string TemplateFileName = "mod-config.default.json";

    private static readonly string[] FormerlyCommentedDifficultyKeys =
    {
        "playerReceivedDamage",
        "playerTroopsReceivedDamage",
        "combatAIDifficulty",
        "recruitmentDifficulty",
        "playerMapMovementSpeed",
        "stealthAndDisguiseDifficulty",
        "persuasionSuccessChance",
        "clanMemberDeathChance",
        "battleDeath",
        "birthAndDeath",
        "autoAllocateClanMemberPerks",
    };

    private static readonly IDictionary<string, JToken> DifficultyDefaults =
        new Dictionary<string, JToken>(StringComparer.OrdinalIgnoreCase)
        {
            ["playerReceivedDamage"] = "Realistic",
            ["playerTroopsReceivedDamage"] = "VeryEasy",
            ["combatAIDifficulty"] = "VeryEasy",
            ["recruitmentDifficulty"] = "VeryEasy",
            ["playerMapMovementSpeed"] = "VeryEasy",
            ["stealthAndDisguiseDifficulty"] = "VeryEasy",
            ["persuasionSuccessChance"] = "VeryEasy",
            ["clanMemberDeathChance"] = "VeryEasy",
            ["battleDeath"] = "VeryEasy",
            ["birthAndDeath"] = true,
            ["autoAllocateClanMemberPerks"] = false,
        };

    private static readonly ILogger Logger = LogManager.GetLogger<ModConfig>();

    private readonly string directoryOverride;
    private readonly Lazy<ModConfigData> data;

    public ModConfig() : this(null)
    {
    }

    /// <summary>Test seam: this directory wins over discovery.</summary>
    internal ModConfig(string directoryOverride)
    {
        this.directoryOverride = directoryOverride;
        data = new Lazy<ModConfigData>(Load);
    }

    public ModConfigData Data => data.Value;

    private ModConfigData Load()
    {
        string dir = ResolveDirectory();
        if (dir == null)
        {
            return new ModConfigData();
        }

        string path = Path.Combine(dir, FileName);
        try
        {
            if (!File.Exists(path))
            {
                Seed(path);
                if (!File.Exists(path))
                {
                    return new ModConfigData();
                }
            }

            MigrateDifficultySettings(path);

            var loaded = JsonConvert.DeserializeObject<ModConfigData>(File.ReadAllText(path), MakeSettings())
                ?? new ModConfigData();
            Logger.Information("mod-config.json loaded ({Path})", path);
            WarnAllUnknownKeys(loaded);
            return loaded;
        }
        catch (Exception ex)
        {
            // A broken config must not take the session down.
            Logger.Error("mod-config.json UNREADABLE ({Path}) — continuing on defaults: {Reason}", path, ex.Message);
            return new ModConfigData();
        }
    }

    /// <summary>
    /// Activates difficulty settings that older templates shipped as comments.
    /// Existing active values win and entirely missing settings receive their
    /// documented defaults. The edit is deliberately textual so operator comments,
    /// ordering, and unrelated settings remain untouched.
    /// </summary>
    private static void MigrateDifficultySettings(string path)
    {
        string original = File.ReadAllText(path);
        JObject root;
        try
        {
            root = JObject.Parse(original);
        }
        catch
        {
            // Leave malformed input byte-for-byte intact; Load owns its warning and
            // default fallback below.
            return;
        }

        JToken difficultyToken = root.GetValue("difficulty", StringComparison.OrdinalIgnoreCase);
        if (difficultyToken == null)
        {
            int rootBrace = original.IndexOf('{');
            var properties = new List<string>();
            foreach (string key in FormerlyCommentedDifficultyKeys)
            {
                properties.Add("    \"" + key + "\": " +
                    DifficultyDefaults[key].ToString(Formatting.None) + ",");
            }
            string block = Environment.NewLine + "  \"difficulty\": {" +
                Environment.NewLine + string.Join(Environment.NewLine, properties) +
                Environment.NewLine + "  },";
            WriteIfValidMigration(path, original, original.Insert(rootBrace + 1, block));
            return;
        }
        if (!(difficultyToken is JObject difficulty))
        {
            // Do not create a duplicate property beside an operator's invalid
            // difficulty value. The normal loader will report the bad config.
            return;
        }

        Match difficultyMatch = Regex.Match(original,
            "(?:^|[{,])[\\t ]*(?<property>\\\"difficulty\\\"[\\t ]*:[\\t ]*\\{)",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (!difficultyMatch.Success)
        {
            return;
        }
        int openBrace = original.IndexOf('{', difficultyMatch.Groups["property"].Index);
        int closeBrace = FindMatchingBrace(original, openBrace);
        if (closeBrace < 0)
        {
            return;
        }

        string migrated = original;
        var insertions = new List<string>();
        foreach (string key in FormerlyCommentedDifficultyKeys)
        {
            JToken existing = difficulty.GetValue(key, StringComparison.OrdinalIgnoreCase);
            if (existing != null)
            {
                continue;
            }

            string commentedBlock = migrated.Substring(openBrace, closeBrace - openBrace + 1);
            string pattern = "^(?<indent>[\\t ]*)//[\\t ]*(?<setting>\\\"" +
                Regex.Escape(key) + "\\\"[\\t ]*:[\\t ]*)(?<value>\\\"[^\\\"\\r\\n]*\\\"|true|false)(?<rest>[\\t ]*,?.*)$";
            Match commented = Regex.Match(commentedBlock, pattern,
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (commented.Success)
            {
                string replacement = commented.Groups["indent"].Value +
                    commented.Groups["setting"].Value + commented.Groups["value"].Value +
                    commented.Groups["rest"].Value;
                int absolute = openBrace + commented.Index;
                migrated = migrated.Remove(absolute, commented.Length).Insert(absolute, replacement);
                int delta = replacement.Length - commented.Length;
                closeBrace += delta;
                continue;
            }

            insertions.Add("    \"" + key + "\": " +
                DifficultyDefaults[key].ToString(Formatting.None) + ",");
        }

        if (insertions.Count > 0)
        {
            string insertion = Environment.NewLine + string.Join(Environment.NewLine, insertions);
            migrated = migrated.Insert(openBrace + 1, insertion);
        }

        WriteIfValidMigration(path, original, migrated);
    }

    private static void WriteIfValidMigration(string path, string original, string migrated)
    {
        if (migrated == original)
        {
            return;
        }
        try
        {
            // Never replace a parseable user config with a bad migration.
            JObject.Parse(migrated);
            ReplaceFile(path, migrated);
            Logger.Information("mod-config.json migrated in place: legacy/server difficulty activated ({Path})", path);
        }
        catch (Exception ex)
        {
            // A read-only config still needs to load its active user settings.
            Logger.Warning("could not migrate mod-config.json in place ({Path}): {Reason} — loading the existing file",
                path, ex.Message);
        }
    }

    private static void ReplaceFile(string path, string contents)
    {
        string temporary = path + ".migration-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, contents);
            JObject.Parse(File.ReadAllText(temporary));
            File.Replace(temporary, path, null);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static int FindMatchingBrace(string text, int openBrace)
    {
        bool inString = false;
        bool escaped = false;
        bool lineComment = false;
        bool blockComment = false;
        int depth = 0;
        for (int i = openBrace; i < text.Length; i++)
        {
            char current = text[i];
            char next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (lineComment)
            {
                if (current == '\n') lineComment = false;
                continue;
            }
            if (blockComment)
            {
                if (current == '*' && next == '/')
                {
                    blockComment = false;
                    i++;
                }
                continue;
            }
            if (inString)
            {
                if (escaped) escaped = false;
                else if (current == '\\') escaped = true;
                else if (current == '"') inString = false;
                continue;
            }
            if (current == '/' && next == '/')
            {
                lineComment = true;
                i++;
                continue;
            }
            if (current == '/' && next == '*')
            {
                blockComment = true;
                i++;
                continue;
            }
            if (current == '"')
            {
                inString = true;
                continue;
            }
            if (current == '{') depth++;
            else if (current == '}' && --depth == 0) return i;
        }
        return -1;
    }

    private string ResolveDirectory()
    {
        if (directoryOverride != null)
        {
            return directoryOverride;
        }

        // Headless hosts set these just before building the container; reading
        // any earlier silently misses the file. COOP_DATA_DIR is the current DS
        // contract (the shared CoopData folder, or the server's own data dir when
        // an established deployment keeps the file there); BANNERLORD_USER_DIR is
        // kept as the fallback for DS builds that predate it.
        var coopDataDir = Environment.GetEnvironmentVariable("COOP_DATA_DIR");
        if (string.IsNullOrEmpty(coopDataDir) == false)
        {
            return coopDataDir;
        }
        var userDir = Environment.GetEnvironmentVariable("BANNERLORD_USER_DIR");
        if (string.IsNullOrEmpty(userDir) == false)
        {
            return userDir;
        }

        // Via the interface, so a host running its own helper (the dedicated
        // server redirects PlatformFileType.User) resolves to ITS user root.
        // Client-hosted sessions land in the game's own user directory; the
        // mod's files live in a CoopData folder there (beside CoopMapData),
        // shared with a dedicated server running on the same account.
        var helper = TaleWorlds.Library.Common.PlatformFileHelper;
        if (helper != null)
        {
            var userRoot = new PlatformDirectoryPath(PlatformFileType.User, "");
            string probe = helper.GetFileFullPath(new PlatformFilePath(userRoot, FileName));
            if (string.IsNullOrEmpty(probe) == false)
            {
                return Path.Combine(Path.GetDirectoryName(probe), CoopDataFolderName);
            }
        }

        return null;
    }

    private static JsonSerializerSettings MakeSettings()
    {
        return new JsonSerializerSettings
        {
            // "VeryEasy" etc. for DifficultyLevel?; reading is case-insensitive.
            Converters = { new StringEnumConverter() },
            Error = (sender, args) =>
            {
                // Bad value: skip the member, keep the file. Syntax errors stay
                // fatal and fall back to defaults through the catch above.
                if (args.ErrorContext.Member is string member)
                {
                    Logger.Warning("mod-config.json: value for '{Member}' invalid — ignored ({Reason})",
                        member, args.ErrorContext.Error.Message);
                    args.ErrorContext.Handled = true;
                }
            },
        };
    }

    private static void WarnAllUnknownKeys(ModConfigData data)
    {
        if (data.UnknownKeys != null) WarnUnknownKeys(data.UnknownKeys);
        if (data.Difficulty?.UnknownKeys != null) WarnUnknownKeys(data.Difficulty.UnknownKeys, "difficulty ");
        if (data.ModOptions?.UnknownKeys != null) WarnUnknownKeys(data.ModOptions.UnknownKeys, "mod option ");
    }

    private static void WarnUnknownKeys(IDictionary<string, JToken> unknownKeys, string keyType = "")
    {
        foreach (var key in unknownKeys.Keys)
        {
            Logger.Warning("mod-config.json: unknown {keyType}key '{Key}' ignored", keyType, key);
        }
    }

    /// <summary>Copies the template out on first run. Existing files are never
    /// replaced; schema migrations above edit only settings that were previously
    /// shipped as comments.</summary>
    private static void Seed(string path)
    {
        string template = ResolveTemplatePath();
        if (template == null)
        {
            Logger.Warning("mod-config.json not created: the module ships no {Template} beside SubModule.xml " +
                "— running on defaults", TemplateFileName);
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.Copy(template, path);
            Logger.Information("created {Path} from {Template} — edit it to configure the mod", path, template);
        }
        catch (Exception ex)
        {
            Logger.Warning("could not seed mod-config.json ({Path}): {Reason} — running on defaults", path, ex.Message);
        }
    }

    /// <summary>Template sits in the module root, this assembly in its bin\ —
    /// walk up rather than assume a depth (also finds a test build's own copy).</summary>
    private static string ResolveTemplatePath()
    {
        string dir = Path.GetDirectoryName(typeof(ModConfig).Assembly.Location);
        for (int level = 0; level < 3 && string.IsNullOrEmpty(dir) == false; level++)
        {
            string candidate = Path.Combine(dir, TemplateFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}
