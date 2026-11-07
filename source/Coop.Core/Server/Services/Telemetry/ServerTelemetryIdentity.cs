using Common.Logging;
using Common.Network;
using Serilog;
using System;
using System.IO;

namespace Coop.Core.Server.Services.Telemetry;

public interface IServerTelemetryIdentity
{
    string ServerId { get; }
}

/// <summary>Loads the server installation identity from the persistent server data directory.</summary>
public sealed class ServerTelemetryIdentity : IServerTelemetryIdentity
{
    private const string FilePrefix = "telemetry-server-id-";
    private const string FileSuffix = ".txt";
    private static readonly ILogger Logger = LogManager.GetLogger<ServerTelemetryIdentity>();
    private readonly Lazy<string> identity;

    public string ServerId => identity.Value;

    public ServerTelemetryIdentity(INetworkConfig networkConfig)
    {
        identity = new Lazy<string>(() => CreateIdentity(
            ResolveDirectory(
                Environment.GetEnvironmentVariable("COOP_DATA_DIR"),
                Environment.GetEnvironmentVariable("BANNERLORD_USER_DIR"),
                AppContext.BaseDirectory),
            networkConfig.Port,
            () => Guid.NewGuid().ToString("N")));
    }

    internal ServerTelemetryIdentity(string directory, int port, Func<string> createId)
    {
        identity = new Lazy<string>(() => LoadOrCreate(directory, port, createId));
    }

    private static string CreateIdentity(string directory, int port, Func<string> createId)
    {
        try
        {
            return LoadOrCreate(directory, port, createId);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is InvalidDataException ||
            ex is ArgumentException ||
            ex is NotSupportedException)
        {
            // Telemetry must not prevent a server from starting when its optional identity file
            // cannot be persisted. This process will use a fresh presence key in that case.
            Logger.Warning(ex, "Server telemetry identity could not be persisted; using a process identity");
            return Guid.NewGuid().ToString("N");
        }
    }

    internal static string ResolveDirectory(
        string? coopDataDirectory,
        string? userDirectory,
        string applicationBaseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(coopDataDirectory))
            return Path.GetFullPath(coopDataDirectory);

        if (!string.IsNullOrWhiteSpace(userDirectory))
            return Path.GetFullPath(userDirectory);

        return Path.GetFullPath(Path.Combine(
            applicationBaseDirectory,
            "..",
            "..",
            "..",
            "server-data"));
    }

    internal static string LoadOrCreate(string directory, int port, Func<string> createId)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Identity directory cannot be empty.", nameof(directory));
        if (port < 1 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));
        if (createId == null)
            throw new ArgumentNullException(nameof(createId));

        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, FilePrefix + port + FileSuffix);
        try
        {
            if (TryRead(path, out var existing)) return existing;
        }
        catch (InvalidDataException)
        {
            // Replace a truncated or hand-edited file with a fresh identity below.
        }

        string created = createId();
        if (!Guid.TryParseExact(created, "N", out _))
            throw new InvalidDataException("The generated telemetry identity is not a GUID.");

        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, created + Environment.NewLine);
            try
            {
                File.Replace(temporaryPath, path, null);
            }
            catch (FileNotFoundException)
            {
                File.Move(temporaryPath, path);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
        return created;
    }

    private static bool TryRead(string path, out string identity)
    {
        identity = string.Empty;
        if (!File.Exists(path)) return false;

        string value = File.ReadAllText(path).Trim();
        if (!Guid.TryParseExact(value, "N", out _))
            throw new InvalidDataException("The persisted telemetry identity is not a GUID.");

        identity = value;
        return true;
    }
}
