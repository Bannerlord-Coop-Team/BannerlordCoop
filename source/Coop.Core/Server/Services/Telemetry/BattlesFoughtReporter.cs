using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.MapEvents;

namespace Coop.Core.Server.Services.Telemetry;

public interface IBattlesFoughtReporter : IDisposable
{
}

/// <summary>Reports each player battle once during the server session.</summary>
public class BattlesFoughtReporter : IBattlesFoughtReporter
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattlesFoughtReporter>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IBattlesFoughtUploader uploader;
    private readonly CancellationTokenSource cancellation;
    private readonly object reportGate = new object();
    private readonly HashSet<string> reportedMapEvents = new HashSet<string>();

    private bool disposed;

    public BattlesFoughtReporter(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IBattlesFoughtUploader uploader,
        CancellationTokenSource sessionCancellation)
    {
        if (messageBroker == null) throw new ArgumentNullException(nameof(messageBroker));
        if (objectManager == null) throw new ArgumentNullException(nameof(objectManager));
        if (uploader == null) throw new ArgumentNullException(nameof(uploader));
        if (sessionCancellation == null) throw new ArgumentNullException(nameof(sessionCancellation));

        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.uploader = uploader;
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(sessionCancellation.Token);

        messageBroker.Subscribe<PlayerJoinedBattle>(Handle_PlayerJoinedBattle);
    }

    private void Handle_PlayerJoinedBattle(MessagePayload<PlayerJoinedBattle> payload)
    {
        if (!(payload.Who is MapEvent mapEvent) ||
            !objectManager.TryGetIdWithLogging(mapEvent, out var mapEventId))
            return;

        lock (reportGate)
        {
            if (disposed || !reportedMapEvents.Add(mapEventId)) return;
        }

        _ = RecordBattleStartedAsync(mapEventId);
    }

    private async Task RecordBattleStartedAsync(string mapEventId)
    {
        try
        {
            var result = await uploader.RecordBattleStartedAsync(cancellation.Token).ConfigureAwait(false);
            if (!result.Uploaded && result.EndpointConfigured)
            {
                Logger.Warning(
                    "Reporting player battle {MapEventId} failed: {Details}",
                    mapEventId,
                    result.Details);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Reporting player battle {MapEventId} failed", mapEventId);
        }
    }

    public void Dispose()
    {
        lock (reportGate)
        {
            if (disposed) return;

            disposed = true;
            cancellation.Cancel();
            reportedMapEvents.Clear();
        }

        messageBroker.Unsubscribe<PlayerJoinedBattle>(Handle_PlayerJoinedBattle);
        cancellation.Dispose();
    }
}
