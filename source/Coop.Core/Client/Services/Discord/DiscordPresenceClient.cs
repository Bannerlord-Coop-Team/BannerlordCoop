using Common.Logging;
using DiscordRPC;
using Serilog;
using System;
using System.Threading.Tasks;

namespace Coop.Core.Client.Services.Discord;

public interface IDiscordPresenceClient : IDisposable
{
    void SetPresence(string details, string state, DateTime startedAtUtc);
    void SetMainMenu();
    void ClearPresence();
}

/// <summary>Serializes optional Discord work away from the game and network threads.</summary>
public sealed class DiscordPresenceClient : IDiscordPresenceClient
{
    public const string ApplicationId = "1546158363480690738";
    public const string ArtworkKey = "bannerlord_coop";
    private static readonly ILogger Logger = LogManager.GetLogger<DiscordPresenceClient>();
    private readonly object gate = new object();
    private readonly IDiscordRpcConnection connection;
    private Task pending = Task.CompletedTask;
    private RichPresence latestPresence;
    private bool initialized;
    private bool disposed;
    private bool failed;

    public DiscordPresenceClient(IDiscordRpcConnection connection)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));
        this.connection = connection;
        connection.Ready += Handle_Ready;
    }

    internal Task PendingWork
    {
        get { lock (gate) return pending; }
    }

    public void SetPresence(string details, string state, DateTime startedAtUtc)
    {
        Enqueue(() =>
        {
            if (failed) return;
            latestPresence = new RichPresence
            {
                Details = details,
                State = state,
                Timestamps = new Timestamps(startedAtUtc),
                Assets = new Assets { LargeImageKey = ArtworkKey, LargeImageText = "Bannerlord Coop" },
            };
            if (!initialized)
            {
                if (!connection.Initialize())
                    throw new InvalidOperationException("Discord RPC could not initialize");
                initialized = true;
            }
            ApplyLatestPresence();
        });
    }

    public void SetMainMenu() => SetPresence(null, "Main Menu", DateTime.UtcNow);

    public void ClearPresence() => Enqueue(() =>
    {
        latestPresence = null;
        ApplyLatestPresence();
    });

    // Automatic READY synchronization can race our writes; reapply our own snapshot after it completes.
    private void Handle_Ready() => Enqueue(ApplyLatestPresence);

    private void ApplyLatestPresence()
    {
        if (initialized && (!failed || latestPresence == null)) connection.SetPresence(latestPresence);
    }

    private void Enqueue(Action action)
    {
        lock (gate)
        {
            if (disposed) return;
            QueueCore(action);
        }
    }

    private void QueueCore(Action action)
    {
        pending = pending.ContinueWith(_ =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                if (!failed)
                    Logger.Warning(exception, "Discord presence disabled for this session");
                failed = true;
            }
        }, TaskScheduler.Default);
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            QueueCore(() =>
            {
                latestPresence = null;
                connection.Ready -= Handle_Ready;
                // The library's graceful shutdown clears presence and closes its background pipe.
                connection.Dispose();
            });
        }
    }
}
