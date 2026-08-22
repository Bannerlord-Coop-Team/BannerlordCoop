using Common.Logging;
using Serilog;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace GameInterface.Services.Entity;

public interface IControllerIdStore
{
    string GetOrCreateId();
}

/// <summary>
/// Persists the fallback identity used when a platform does not provide a usable account id.
/// </summary>
public class ControllerIdStore : IControllerIdStore
{
    private const int AccessAttemptCount = 20;
    private const int AccessRetryDelayMilliseconds = 25;
    private const string FileName = "controller-id.txt";

    private static readonly ILogger Logger = LogManager.GetLogger<ControllerIdStore>();

    private readonly Action accessRetryDelay;
    private readonly string filePath;
    private readonly object gate = new object();
    private string controllerId;

    public ControllerIdStore() : this(GetDefaultFilePath())
    {
    }

    internal ControllerIdStore(string filePath) : this(
        filePath,
        () => Thread.Sleep(AccessRetryDelayMilliseconds))
    {
    }

    internal ControllerIdStore(string filePath, Action accessRetryDelay)
    {
        if (accessRetryDelay == null) throw new ArgumentNullException(nameof(accessRetryDelay));

        this.filePath = filePath;
        this.accessRetryDelay = accessRetryDelay;
    }

    public string GetOrCreateId()
    {
        lock (gate)
        {
            if (controllerId != null) return controllerId;

            controllerId = ReadOrCreatePersistedId();
            return controllerId;
        }
    }

    private string ReadOrCreatePersistedId()
    {
        string directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        IOException lastException = null;
        for (int attempt = 0; attempt < AccessAttemptCount; attempt++)
        {
            try
            {
                using var stream = new FileStream(
                    filePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
                return ReadOrCreateId(stream);
            }
            catch (IOException ex)
            {
                lastException = ex;
                if (attempt + 1 < AccessAttemptCount)
                {
                    accessRetryDelay();
                }
            }
        }

        throw new InvalidOperationException(
            $"Could not persist fallback controller id ({filePath})",
            lastException);
    }

    private string ReadOrCreateId(FileStream stream)
    {
        string value;
        using (var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true))
        {
            value = reader.ReadToEnd().Trim();
        }

        if (Guid.TryParse(value, out Guid parsedId)) return parsedId.ToString("N");

        if (!string.IsNullOrEmpty(value))
        {
            Logger.Warning("Controller id file was invalid and will be replaced ({Path})", filePath);
        }

        string id = Guid.NewGuid().ToString("N");
        stream.Position = 0;
        stream.SetLength(0);
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true))
        {
            writer.Write(id);
            writer.Flush();
        }

        stream.Flush();
        return id;
    }

    private static string GetDefaultFilePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Mount and Blade II Bannerlord",
            "Configs",
            "BannerlordCoop",
            FileName);
    }
}
