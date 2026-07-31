using Common.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Serilog;
using System;
using System.IO;
using TaleWorlds.Library;

namespace GameInterface.Configuration;

/// <summary>
/// Loads mod-config.json from %BANNERLORD_USER_DIR%, else the engine's user
/// directory; seeds it from the module's template when absent, and falls back to
/// defaults if it cannot be read. Never touches the dedicated server's
/// server-config.json — the two configs are independent by design.
/// </summary>
internal sealed class ModConfig : IModConfig
{
    private const string FileName = "mod-config.json";

    /// <summary>Ships in the module root — the only copy of the defaults.</summary>
    private const string TemplateFileName = "mod-config.default.json";

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
                // Template keys are all commented out: identical to defaults.
                return new ModConfigData();
            }

            var loaded = JsonConvert.DeserializeObject<ModConfigData>(File.ReadAllText(path), MakeSettings())
                ?? new ModConfigData();
            Logger.Information("mod-config.json loaded ({Path})", path);
            WarnUnknownKeys(loaded);
            return loaded;
        }
        catch (Exception ex)
        {
            // A broken config must not take the session down.
            Logger.Error("mod-config.json UNREADABLE ({Path}) — continuing on defaults: {Reason}", path, ex.Message);
            return new ModConfigData();
        }
    }

    private string ResolveDirectory()
    {
        if (directoryOverride != null)
        {
            return directoryOverride;
        }

        // Headless hosts set this just before building the container; reading
        // any earlier silently misses the file.
        var userDir = Environment.GetEnvironmentVariable("BANNERLORD_USER_DIR");
        if (string.IsNullOrEmpty(userDir) == false)
        {
            return userDir;
        }

        // Via the interface, so a host running its own helper (the dedicated
        // server redirects PlatformFileType.User) resolves to ITS user root.
        var helper = TaleWorlds.Library.Common.PlatformFileHelper;
        if (helper != null)
        {
            var userRoot = new PlatformDirectoryPath(PlatformFileType.User, "");
            string probe = helper.GetFileFullPath(new PlatformFilePath(userRoot, FileName));
            if (string.IsNullOrEmpty(probe) == false)
            {
                return Path.GetDirectoryName(probe);
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

    private static void WarnUnknownKeys(ModConfigData data)
    {
        if (data.UnknownKeys != null)
        {
            foreach (var key in data.UnknownKeys.Keys)
            {
                Logger.Warning("mod-config.json: unknown key '{Key}' ignored", key);
            }
        }
        if (data.Difficulty?.UnknownKeys != null)
        {
            foreach (var key in data.Difficulty.UnknownKeys.Keys)
            {
                Logger.Warning("mod-config.json: unknown difficulty key '{Key}' ignored", key);
            }
        }
    }

    /// <summary>Copies the template out. Every key in it is commented out, so
    /// seeding can never change an existing world.</summary>
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
