using Common;
using Common.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
/// settings and, on the server, adding settings new in the template, without
/// replacing active operator values.
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
            ["playerReceivedDamage"] = "VeryEasy",
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
    private readonly string templatePathOverride;
    private readonly Lazy<ModConfigData> data;

    public ModConfig() : this(null)
    {
    }

    /// <summary>Test seam: this directory, and this template file, win over discovery.</summary>
    internal ModConfig(string directoryOverride, string templatePathOverride = null)
    {
        this.directoryOverride = directoryOverride;
        this.templatePathOverride = templatePathOverride;
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
        string template = templatePathOverride ?? ResolveTemplatePath();
        try
        {
            if (!File.Exists(path))
            {
                Seed(path, template);
                if (!File.Exists(path))
                {
                    return new ModConfigData();
                }
            }

            MigrateDifficultySettings(path);
            // A player-hosted session's clients share the server's file, and remote clients don't need server options.
            if (ModInformation.IsServer) AddNewTemplateSettings(path, template);

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
            WriteIfValidMigration(path, original, original.Insert(rootBrace + 1, block), "missing settings activated");
            return;
        }
        if (!(difficultyToken is JObject difficulty))
        {
            // Do not create a duplicate property beside an operator's invalid
            // difficulty value. The normal loader will report the bad config.
            return;
        }

        int openBrace = FindBlockOpenBrace(original, "difficulty");
        if (openBrace < 0)
        {
            return;
        }
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

        WriteIfValidMigration(path, original, migrated, "missing settings activated");
    }

    /// <summary>
    /// Copies settings that are new in the shipped template into an existing file: each top-level block,
    /// and each key inside a block, that the file lacks, with the template's comment lines above it.
    /// A key the operator commented out counts as present. The edit is textual so operator values,
    /// comments and ordering stay as they are.
    /// </summary>
    private static void AddNewTemplateSettings(string path, string templatePath)
    {
        if (templatePath == null) return;

        string original = File.ReadAllText(path);
        string template;
        try
        {
            template = File.ReadAllText(templatePath);
            JObject.Parse(original);
            JObject.Parse(template);
        }
        catch
        {
            // Leave malformed input intact; Load reports a broken config.
            return;
        }

        string newLine = original.Contains("\r\n") ? "\r\n" : "\n";
        var edits = new List<(int Offset, string Text)>();
        var added = new List<string>();
        AddMissingMembers(original, SkipTrivia(original, 0), template, SkipTrivia(template, 0), "", newLine, edits, added);
        if (added.Count == 0) return;

        string merged = original;
        foreach (var edit in edits.OrderByDescending(edit => edit.Offset))
        {
            merged = merged.Insert(edit.Offset, edit.Text);
        }
        WriteIfValidMigration(path, original, merged, "added settings new in the template: " + string.Join(", ", added));
    }

    // Queues an insertion for each template member the object at open lacks, placed after the previous template
    // member it has, or at the top of the object. The root call also fills in the blocks both have.
    private static void AddMissingMembers(string text, int open, string template, int templateOpen, string prefix,
        string newLine, List<(int Offset, string Text)> edits, List<string> added)
    {
        var members = ReadMembers(text, open, out int close);
        var templateMembers = ReadMembers(template, templateOpen, out _);
        if (members == null || templateMembers == null) return;

        bool StartsWithComment(string snippet) => snippet.TrimStart().StartsWith("//", StringComparison.Ordinal);
        var pending = new List<string>();

        // Inserts the pending snippets after members[previous], or after the opening brace when previous is -1.
        void InsertPendingAfter(int previous)
        {
            if (pending.Count == 0) return;

            bool firstHasComment = StartsWithComment(pending[0]);
            bool lastHasComment = StartsWithComment(pending[pending.Count - 1]);
            string snippets = pending[0];
            for (int i = 1; i < pending.Count; i++)
            {
                snippets += "," + newLine + (StartsWithComment(pending[i]) ? newLine : "") + pending[i];
            }
            bool hasFollowing = previous + 1 < members.Count;
            if (hasFollowing) snippets += ",";
            pending.Clear();

            int end = previous < 0 ? open + 1 : members[previous].End;
            bool needsComma = previous >= 0 && text[end - 1] != ',';
            int lineEnd = text.IndexOf('\n', end);
            if (lineEnd < 0 || lineEnd > (hasFollowing ? members[previous + 1].NameStart : close))
            {
                // Something follows on the same line.
                edits.Add((end, (needsComma ? "," : "") + newLine + snippets));
                return;
            }

            // Insert at the end of the line, after any comment trailing it, with commented settings set apart by blank lines.
            if (text[lineEnd - 1] == '\r') lineEnd--;
            int nextStart = text.IndexOf('\n', lineEnd) + 1;
            int nextEnd = text.IndexOf('\n', nextStart);
            string nextLine = text.Substring(nextStart, (nextEnd < 0 ? text.Length : nextEnd) - nextStart).Trim();
            bool blankBefore = previous >= 0 && firstHasComment;
            bool blankAfter = hasFollowing && nextLine.Length > 0 &&
                (lastHasComment || nextLine.StartsWith("//", StringComparison.Ordinal));
            string insertion = newLine + (blankBefore ? newLine : "") + snippets + (blankAfter ? newLine : "");
            if (needsComma && lineEnd == end) insertion = "," + insertion;
            else if (needsComma) edits.Add((end, ","));
            edits.Add((lineEnd, insertion));
        }

        string block = text.Substring(open, close - open + 1);
        int previousIndex = -1;
        foreach (var templateMember in templateMembers)
        {
            int index = members.FindIndex(member =>
                string.Equals(member.Name, templateMember.Name, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                bool commentedOut = Regex.IsMatch(block,
                    "^[\\t ]*//[\\t ]*\\\"" + Regex.Escape(templateMember.Name) + "\\\"[\\t ]*:",
                    RegexOptions.Multiline | RegexOptions.IgnoreCase);
                if (commentedOut) continue;

                int start = MemberStart(template, templateMember.NameStart);
                pending.Add(template.Substring(start, templateMember.ValueEnd - start)
                    .Replace("\r\n", "\n").Replace("\n", newLine));
                added.Add(prefix + templateMember.Name);
                continue;
            }

            InsertPendingAfter(previousIndex);
            previousIndex = index;

            var existing = members[index];
            if (prefix.Length == 0 && text[existing.ValueStart] == '{' && template[templateMember.ValueStart] == '{')
            {
                AddMissingMembers(text, existing.ValueStart, template, templateMember.ValueStart,
                    templateMember.Name + ".", newLine, edits, added);
            }
        }
        InsertPendingAfter(previousIndex);
    }

    // Lists the members of the object whose { is at open, or null when it isn't plain quoted members.
    // End is just past the member's value, or past its comma.
    private static List<(string Name, int NameStart, int ValueStart, int ValueEnd, int End)> ReadMembers(
        string text, int open, out int close)
    {
        var members = new List<(string Name, int NameStart, int ValueStart, int ValueEnd, int End)>();
        close = -1;
        int i = SkipTrivia(text, open + 1);
        while (i < text.Length && text[i] == '"')
        {
            int nameStart = i;
            int nameEnd = SkipValue(text, nameStart);
            if (nameEnd < 0) return null;
            int colon = SkipTrivia(text, nameEnd);
            if (colon >= text.Length || text[colon] != ':') return null;
            int valueStart = SkipTrivia(text, colon + 1);
            int valueEnd = SkipValue(text, valueStart);
            if (valueEnd < 0) return null;

            int end = valueEnd;
            i = SkipTrivia(text, valueEnd);
            if (i < text.Length && text[i] == ',')
            {
                end = i + 1;
                i = SkipTrivia(text, i + 1);
            }
            members.Add((text.Substring(nameStart + 1, nameEnd - nameStart - 2), nameStart, valueStart, valueEnd, end));
        }
        if (i >= text.Length || text[i] != '}') return null;

        close = i;
        return members;
    }

    // Index just past the JSON value that starts at start, or -1 when there is none.
    private static int SkipValue(string text, int start)
    {
        if (start >= text.Length) return -1;

        char first = text[start];
        if (first == '{' || first == '[')
        {
            int closing = FindMatchingBrace(text, start);
            return closing < 0 ? -1 : closing + 1;
        }
        if (first == '"')
        {
            for (int i = start + 1; i < text.Length; i++)
            {
                if (text[i] == '\\') i++;
                else if (text[i] == '"') return i + 1;
            }
            return -1;
        }

        int end = start;
        while (end < text.Length && ",}]/".IndexOf(text[end]) < 0 && !char.IsWhiteSpace(text[end])) end++;
        return end > start ? end : -1;
    }

    // Index of the next character that isn't whitespace or a comment.
    private static int SkipTrivia(string text, int i)
    {
        while (i < text.Length)
        {
            char next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (char.IsWhiteSpace(text[i]))
            {
                i++;
            }
            else if (text[i] == '/' && next == '/')
            {
                int lineEnd = text.IndexOf('\n', i);
                i = lineEnd < 0 ? text.Length : lineEnd + 1;
            }
            else if (text[i] == '/' && next == '*')
            {
                int commentEnd = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = commentEnd < 0 ? text.Length : commentEnd + 2;
            }
            else
            {
                break;
            }
        }
        return i;
    }

    // Where a member's text begins: its line, moved up over the // comment lines directly above it,
    // or the member itself when something precedes it on its line.
    private static int MemberStart(string text, int nameStart)
    {
        int lineStart = text.LastIndexOf('\n', nameStart - 1) + 1;
        if (!string.IsNullOrWhiteSpace(text.Substring(lineStart, nameStart - lineStart))) return nameStart;

        while (lineStart > 1)
        {
            int previous = text.LastIndexOf('\n', lineStart - 2) + 1;
            if (!text.Substring(previous, lineStart - previous).TrimStart().StartsWith("//", StringComparison.Ordinal)) break;
            lineStart = previous;
        }
        return lineStart;
    }

    // Finds the opening brace of the "name": { block, or -1 when the text has none.
    private static int FindBlockOpenBrace(string text, string blockName)
    {
        Match match = Regex.Match(text,
            "(?:^|[{,])[\\t ]*(?<property>\\\"" + Regex.Escape(blockName) + "\\\"[\\t ]*:[\\t ]*\\{)",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (!match.Success) return -1;

        return text.IndexOf('{', match.Groups["property"].Index);
    }

    private static void WriteIfValidMigration(string path, string original, string migrated, string change)
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
            Logger.Information("mod-config.json migrated in place: {Change} ({Path})", change, path);
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
            if (current == '{' || current == '[') depth++;
            else if ((current == '}' || current == ']') && --depth == 0) return i;
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
            // Existing configs may retain the retired local movement settings block.
            if (string.IsNullOrEmpty(keyType) &&
                string.Equals(key, "network", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Logger.Warning("mod-config.json: unknown {keyType}key '{Key}' ignored", keyType, key);
        }
    }

    /// <summary>Copies the template out on first run. Existing files are never
    /// replaced; the migrations above only activate formerly commented settings
    /// and add settings new in the template.</summary>
    private static void Seed(string path, string template)
    {
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
