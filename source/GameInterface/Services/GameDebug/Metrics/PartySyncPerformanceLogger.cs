using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.GameDebug.Metrics;

internal interface IPartySyncPerformanceClock
{
    DateTime UtcNow { get; }
}

internal class PartySyncPerformanceClock : IPartySyncPerformanceClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

internal interface IPartySyncPerformanceFileWriter
{
    void WriteAllText(string path, string contents);
    void AppendAllText(string path, string contents);
}

internal class PartySyncPerformanceFileWriter : IPartySyncPerformanceFileWriter
{
    private static readonly Encoding Encoding = new UTF8Encoding(false);

    public void WriteAllText(string path, string contents)
    {
        File.WriteAllText(path, contents, Encoding);
    }

    public void AppendAllText(string path, string contents)
    {
        File.AppendAllText(path, contents, Encoding);
    }
}

internal interface IPartySyncPerformancePartyProvider
{
    PartySyncPerformanceData[] GetPartyData();
}

internal class PartySyncPerformancePartyProvider : IPartySyncPerformancePartyProvider
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartySyncPerformancePartyProvider>();

    private readonly IObjectManager objectManager;

    public PartySyncPerformancePartyProvider(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public PartySyncPerformanceData[] GetPartyData()
    {
        var parties = Campaign.Current?.CampaignObjectManager?.MobileParties;
        if (parties == null) return Array.Empty<PartySyncPerformanceData>();

        return GetPartyData(parties);
    }

    internal PartySyncPerformanceData[] GetPartyData(IEnumerable<MobileParty> parties)
    {
        var result = new List<PartySyncPerformanceData>();

        foreach (var party in parties)
        {
            if (!objectManager.TryGetId(party, out var partyId))
            {
                Logger.Warning("Skipping party sync performance snapshot for unresolved party type {PartyType}", party?.GetType().Name);
                continue;
            }

            result.Add(new PartySyncPerformanceData(partyId, party.Position));
        }

        return result.ToArray();
    }
}

internal class PartySyncPerformanceLogger : IPartySyncPerformanceLogger, IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartySyncPerformanceLogger>();

    private const string Header =
        "timestamp_utc,interval_seconds,server_party_count,client_party_count,matched_count,missing_on_client_count,missing_on_server_count,average_matched_distance,max_matched_distance,total_matched_distance";

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly INetworkConfig configuration;
    private readonly IPartySyncPerformancePartyProvider partyProvider;
    private readonly IPartySyncPerformanceClock clock;
    private readonly IPartySyncPerformanceFileWriter fileWriter;
    private readonly object gate = new object();

    private Timer timer;
    private bool requestPending;
    private DateTime requestStartedUtc;
    private int requestId;
    private int pendingRequestId;
    private int skippedPendingRequests;
    private TimeSpan interval;
    private string filePath;

    public PartySyncPerformanceLogger(
        IMessageBroker messageBroker,
        INetwork network,
        INetworkConfig configuration,
        IPartySyncPerformancePartyProvider partyProvider,
        IPartySyncPerformanceClock clock,
        IPartySyncPerformanceFileWriter fileWriter)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.configuration = configuration;
        this.partyProvider = partyProvider;
        this.clock = clock;
        this.fileWriter = fileWriter;

        messageBroker.Subscribe<GameLoadStarted>(Handle_Reset);
        messageBroker.Subscribe<CampaignReady>(Handle_Reset);
        messageBroker.Subscribe<MainMenuEntered>(Handle_Reset);
    }

    public string Enable(TimeSpan interval, string fileName)
    {
        if (interval <= TimeSpan.Zero)
        {
            return "Interval must be greater than 0 seconds";
        }

        if (!TryNormalizeFileName(fileName, out var normalizedFileName, out var error))
        {
            return error;
        }

        lock (gate)
        {
            StopTimer();

            this.interval = interval;
            filePath = normalizedFileName;
            requestPending = false;
            pendingRequestId = 0;
            skippedPendingRequests = 0;

            fileWriter.WriteAllText(filePath, Header + Environment.NewLine);

            timer = new Timer(interval.TotalMilliseconds) { AutoReset = false };
            timer.Elapsed += Handle_TimerElapsed;
            timer.Start();
        }

        return $"Party sync performance logs are ON. Writing every {interval.TotalSeconds:0.###} seconds to {normalizedFileName}";
    }

    public string Disable()
    {
        lock (gate)
        {
            ResetLocked();
        }

        return "Party sync performance logs are OFF";
    }

    public string Status()
    {
        lock (gate)
        {
            if (timer == null)
            {
                return "Party sync performance logs are OFF";
            }

            return $"Party sync performance logs are ON. Interval: {interval.TotalSeconds:0.###} seconds. File: {filePath}. Pending: {requestPending}. Skipped pending requests: {skippedPendingRequests}.";
        }
    }

    public void HandleSnapshot(NetworkPartySyncPerformanceSnapshot snapshot)
    {
        if (GameThread.Instance.IsInitialized && !GameThread.Instance.IsGameThread)
        {
            GameThread.RunSafe(() => HandleSnapshotOnGameThread(snapshot), context: "PartySyncPerformanceLogger.HandleSnapshot");
            return;
        }

        HandleSnapshotOnGameThread(snapshot);
    }

    internal void RequestSnapshot()
    {
        NetworkRequestPartySyncPerformanceSnapshot request;

        lock (gate)
        {
            if (timer == null) return;

            if (requestPending)
            {
                if (clock.UtcNow - requestStartedUtc < configuration.AuditTimeout)
                {
                    skippedPendingRequests++;
                    Logger.Warning(
                        "Skipping party sync performance snapshot request because request {RequestId} is still pending. Skipped {SkippedCount} pending request(s).",
                        pendingRequestId,
                        skippedPendingRequests);
                    return;
                }

                Logger.Warning(
                    "Party sync performance snapshot request {RequestId} timed out after {TimeoutSeconds:0.###} seconds",
                    pendingRequestId,
                    configuration.AuditTimeout.TotalSeconds);
            }

            pendingRequestId = ++requestId;
            requestStartedUtc = clock.UtcNow;
            requestPending = true;
            request = new NetworkRequestPartySyncPerformanceSnapshot(pendingRequestId);
        }

        network.SendAll(request);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<GameLoadStarted>(Handle_Reset);
        messageBroker.Unsubscribe<CampaignReady>(Handle_Reset);
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_Reset);

        lock (gate)
        {
            ResetLocked();
        }
    }

    private void Handle_TimerElapsed(object sender, ElapsedEventArgs e)
    {
        try
        {
            RequestSnapshot();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to request party sync performance snapshot");
        }
        finally
        {
            lock (gate)
            {
                timer?.Start();
            }
        }
    }

    internal void HandleSnapshotOnGameThread(NetworkPartySyncPerformanceSnapshot snapshot)
    {
        string targetFilePath;
        TimeSpan currentInterval;

        lock (gate)
        {
            if (timer == null) return;
            if (!requestPending) return;
            if (snapshot.RequestId != pendingRequestId)
            {
                Logger.Warning(
                    "Ignoring stale party sync performance snapshot response {ResponseRequestId}; pending request is {PendingRequestId}",
                    snapshot.RequestId,
                    pendingRequestId);
                return;
            }

            requestPending = false;
            targetFilePath = filePath;
            currentInterval = interval;
        }

        var clientData = partyProvider.GetPartyData();
        var row = BuildCsvRow(snapshot.Data ?? Array.Empty<PartySyncPerformanceData>(), clientData, currentInterval);

        fileWriter.AppendAllText(targetFilePath, row + Environment.NewLine);
    }

    private string BuildCsvRow(
        PartySyncPerformanceData[] serverData,
        PartySyncPerformanceData[] clientData,
        TimeSpan currentInterval)
    {
        var serverById = BuildDictionary(serverData);
        var clientById = BuildDictionary(clientData);
        var matchedCount = 0;
        var missingOnClient = 0;
        double totalDistance = 0d;
        double maxDistance = 0d;

        foreach (var serverParty in serverById)
        {
            if (!clientById.TryGetValue(serverParty.Key, out var clientParty))
            {
                missingOnClient++;
                continue;
            }

            var distance = serverParty.Value.Position.ToVec2().Distance(clientParty.Position.ToVec2());
            matchedCount++;
            totalDistance += distance;
            if (distance > maxDistance)
            {
                maxDistance = distance;
            }
        }

        var missingOnServer = clientById.Keys.Count(id => !serverById.ContainsKey(id));
        var averageDistance = matchedCount == 0 ? 0d : totalDistance / matchedCount;

        return string.Join(",",
            clock.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            currentInterval.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture),
            serverById.Count.ToString(CultureInfo.InvariantCulture),
            clientById.Count.ToString(CultureInfo.InvariantCulture),
            matchedCount.ToString(CultureInfo.InvariantCulture),
            missingOnClient.ToString(CultureInfo.InvariantCulture),
            missingOnServer.ToString(CultureInfo.InvariantCulture),
            averageDistance.ToString("0.######", CultureInfo.InvariantCulture),
            maxDistance.ToString("0.######", CultureInfo.InvariantCulture),
            totalDistance.ToString("0.######", CultureInfo.InvariantCulture));
    }

    private void Handle_Reset<T>(MessagePayload<T> payload) where T : IMessage
    {
        lock (gate)
        {
            ResetLocked();
        }
    }

    private void ResetLocked()
    {
        StopTimer();
        requestPending = false;
        pendingRequestId = 0;
        skippedPendingRequests = 0;
        filePath = null;
        interval = TimeSpan.Zero;
    }

    private void StopTimer()
    {
        if (timer == null) return;

        timer.Elapsed -= Handle_TimerElapsed;
        timer.Stop();
        timer.Dispose();
        timer = null;
    }

    private static Dictionary<string, PartySyncPerformanceData> BuildDictionary(PartySyncPerformanceData[] data)
    {
        var result = new Dictionary<string, PartySyncPerformanceData>();

        foreach (var item in data)
        {
            if (string.IsNullOrWhiteSpace(item?.MobilePartyId)) continue;
            if (result.ContainsKey(item.MobilePartyId)) continue;

            result.Add(item.MobilePartyId, item);
        }

        return result;
    }

    private static bool TryNormalizeFileName(string fileName, out string normalizedFileName, out string error)
    {
        normalizedFileName = null;
        error = null;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            error = "Usage: coop.debug.metrics.party_sync_performance_logs on <seconds> <filename>";
            return false;
        }

        if (Path.GetFileName(fileName) != fileName || fileName.Contains(".."))
        {
            error = "Filename must not include a path";
            return false;
        }

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            error = "Filename contains invalid characters";
            return false;
        }

        normalizedFileName = fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : fileName + ".csv";

        return true;
    }
}
