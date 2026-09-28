using Common.Logging;
using GameInterface.Services.Chat.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Coop.Core.Common.Configuration;

/// <summary>Operator-written details the server shares with joining players.</summary>
public interface IServerInfoConfig
{
    /// <summary>Lines sent as System chat to each player after their campaign sync.</summary>
    IReadOnlyList<string> Motd { get; }
}

/// <summary>
/// Reads the optional server-info.json once per server container. A missing file gives no MOTD,
/// and an unreadable one is logged and ignored.
/// </summary>
internal sealed class ServerInfoConfig : IServerInfoConfig
{
    internal const int MaxMotdLines = 5;
    private const string FileName = "server-info.json";
    private const string FileEnvironmentVariable = "COOP_SERVER_INFO_FILE";

    private static readonly ILogger Logger = LogManager.GetLogger<ServerInfoConfig>();

    public ServerInfoConfig(IServerDataPath dataPath) : this(() => dataPath.Resolve(FileEnvironmentVariable, FileName))
    {
    }

    internal ServerInfoConfig(string path) : this(() => path)
    {
    }

    private ServerInfoConfig(Func<string> resolvePath)
    {
        if (resolvePath == null) throw new ArgumentNullException(nameof(resolvePath));
        Motd = Load(resolvePath);
    }

    public IReadOnlyList<string> Motd { get; }

    private static IReadOnlyList<string> Load(Func<string> resolvePath)
    {
        string path = string.Empty;

        try
        {
            path = resolvePath();

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (FileNotFoundException)
            {
                return Array.Empty<string>();
            }
            catch (DirectoryNotFoundException)
            {
                return Array.Empty<string>();
            }

            using JsonDocument document = JsonDocument.Parse(json);
            IReadOnlyList<string> motd = ReadMotd(document.RootElement, path);
            Logger.Information("Server info loaded from {Path} (motd {Count} line(s))", path, motd.Count);
            return motd;
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is JsonException ||
            exception is ArgumentException ||
            exception is NotSupportedException)
        {
            Logger.Error(exception, "Server info could not be read from {Path}; running without a MOTD", path);
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyList<string> ReadMotd(JsonElement root, string path)
    {
        // A root that is not an object leaves motd undefined, so it gets the warning below.
        JsonElement motd = default;
        if (root.ValueKind == JsonValueKind.Object && !root.TryGetProperty("motd", out motd))
        {
            return Array.Empty<string>();
        }

        if (motd.ValueKind != JsonValueKind.Array)
        {
            Logger.Warning("Server info in {Path} has no motd array of strings; running without a MOTD", path);
            return Array.Empty<string>();
        }

        var lines = new List<string>();
        int skipped = 0;
        bool trimmed = false;
        foreach (JsonElement entry in motd.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.String)
            {
                skipped++;
                continue;
            }

            string line = RemoveControlCharacters(entry.GetString());
            if (line.Length == 0) continue;

            if (lines.Count == MaxMotdLines)
            {
                trimmed = true;
                break;
            }

            if (line.Length > ChatMessageLimits.MaxMessageLength)
            {
                line = Truncate(line);
                trimmed = true;
            }

            lines.Add(line);
        }

        if (skipped > 0)
        {
            Logger.Warning("Server info motd in {Path} has {Count} entry(s) that are not strings; skipping them", path, skipped);
        }

        if (trimmed)
        {
            Logger.Warning(
                "Server info motd in {Path} was cut to {MaxLines} line(s) of at most {MaxLength} characters",
                path,
                MaxMotdLines,
                ChatMessageLimits.MaxMessageLength);
        }

        return lines.ToArray();
    }

    private static string RemoveControlCharacters(string text)
    {
        var characters = text.ToCharArray();
        for (int i = 0; i < characters.Length; i++)
        {
            if (char.IsControl(characters[i])) characters[i] = ' ';
        }

        return new string(characters).Trim();
    }

    private static string Truncate(string line)
    {
        int length = ChatMessageLimits.MaxMessageLength;
        // Cut before a surrogate pair instead of sending half of it.
        if (char.IsHighSurrogate(line[length - 1])) length--;

        return line.Substring(0, length).TrimEnd();
    }
}
