using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Coop.Core.Server.Connections;

/// <summary>Checks server-managed Steam64 identities before player resolution.</summary>
public interface ISteamBanList
{
    bool IsBanned(string controllerId);
}

/// <summary>
/// Reads the host's Steam64 ban list and refreshes it when the file changes.
/// </summary>
internal sealed class SteamBanList : ISteamBanList
{
    private const string FileName = "steam-bans.json";
    private const string BanFileEnvironmentVariable = "COOP_STEAM_BAN_FILE";

    private static readonly ILogger Logger = LogManager.GetLogger<SteamBanList>();

    private readonly object sync = new object();
    private readonly Func<string> resolvePath;
    private HashSet<string> bannedIds = new HashSet<string>(StringComparer.Ordinal);
    private string loadedPath = string.Empty;
    private DateTime loadedWriteTimeUtc = DateTime.MinValue;
    private bool fileWasPresent;

    public SteamBanList() : this(ResolvePath)
    {
    }

    internal SteamBanList(string path) : this(() => path)
    {
    }

    private SteamBanList(Func<string> resolvePath)
    {
        if (resolvePath == null) throw new ArgumentNullException(nameof(resolvePath));
        this.resolvePath = resolvePath;
    }

    public bool IsBanned(string controllerId)
    {
        string normalized = Normalize(controllerId);
        if (normalized == null) return false;

        ReloadIfChanged();

        lock (sync)
        {
            return bannedIds.Contains(normalized);
        }
    }

    internal static string? Normalize(string? steamId)
    {
        if (string.IsNullOrWhiteSpace(steamId)) return null;

        string trimmed = steamId.Trim();
        if (trimmed.Length < 5 || trimmed.Length > 20) return null;
        return trimmed.All(char.IsDigit) ? trimmed : null;
    }

    private void ReloadIfChanged()
    {
        string path = resolvePath();

        try
        {
            try
            {
                File.GetAttributes(path);
            }
            catch (FileNotFoundException)
            {
                ClearMissingFile(path);
                return;
            }
            catch (DirectoryNotFoundException)
            {
                ClearMissingFile(path);
                return;
            }

            DateTime writeTimeUtc = File.GetLastWriteTimeUtc(path);
            lock (sync)
            {
                if (path == loadedPath && fileWasPresent && writeTimeUtc == loadedWriteTimeUtc) return;
            }

            HashSet<string> next = Read(path);
            lock (sync)
            {
                bannedIds = next;
                loadedPath = path;
                loadedWriteTimeUtc = writeTimeUtc;
                fileWasPresent = true;
            }

            Logger.Information("Steam ban list loaded from {Path} ({Count} id(s))", path, next.Count);
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is JsonException)
        {
            Logger.Error(exception, "Steam ban list could not be reloaded from {Path}; keeping the previous list", path);
        }
    }

    private void ClearMissingFile(string path)
    {
        lock (sync)
        {
            if (path == loadedPath && !fileWasPresent) return;

            bannedIds = new HashSet<string>(StringComparer.Ordinal);
            loadedPath = path;
            loadedWriteTimeUtc = DateTime.MinValue;
            fileWasPresent = false;
        }
    }

    private static HashSet<string> Read(string path)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.ValueKind != JsonValueKind.Object) return result;

        if (document.RootElement.TryGetProperty("steamIds", out JsonElement steamIds) &&
            steamIds.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement entry in steamIds.EnumerateArray())
            {
                AddNormalized(result, entry.ValueKind == JsonValueKind.String ? entry.GetString() : null);
            }
        }

        if (document.RootElement.TryGetProperty("entries", out JsonElement entries) &&
            entries.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object ||
                    !entry.TryGetProperty("steamId", out JsonElement steamId) ||
                    steamId.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                AddNormalized(result, steamId.GetString());
            }
        }

        return result;
    }

    private static void AddNormalized(HashSet<string> result, string? steamId)
    {
        string normalized = Normalize(steamId);
        if (normalized != null) result.Add(normalized);
    }

    private static string ResolvePath()
    {
        string? configuredPath = Environment.GetEnvironmentVariable(BanFileEnvironmentVariable);
        string? coopDataDirectory = Environment.GetEnvironmentVariable("COOP_DATA_DIR");
        return ResolvePath(configuredPath, coopDataDirectory, AppContext.BaseDirectory);
    }

    internal static string ResolvePath(
        string? configuredPath,
        string? coopDataDirectory,
        string applicationBaseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath)) return Path.GetFullPath(configuredPath);

        if (!string.IsNullOrWhiteSpace(coopDataDirectory))
        {
            return Path.Combine(coopDataDirectory, FileName);
        }

        string deploymentPath = Path.GetFullPath(Path.Combine(
            applicationBaseDirectory,
            "..",
            "..",
            "..",
            "server-data",
            FileName));
        return deploymentPath;
    }
}
