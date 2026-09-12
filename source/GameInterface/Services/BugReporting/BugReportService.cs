using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.BugReporting.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.UI.BugReporting;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.Library;

namespace GameInterface.Services.BugReporting;

/// <summary>Starts server-side diagnostic reports that collect logs from consenting clients.</summary>
public interface IBugReportService
{
    void RequestReport(
        string trigger,
        NetPeer requester,
        string summary = null,
        string description = null);
    void SubmitReport(string summary, string description);
}

/// <inheritdoc />
internal class BugReportService : IBugReportService, IDisposable
{
    private const int MaximumCompressedLogBytes = CoopLogSnapshotProvider.MaximumCompressedLogBytes;
    internal const int MaximumCombinedCompressedBytes = 8 * 1024 * 1024;
    private static readonly TimeSpan CollectionTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MinimumCollectionInterval = TimeSpan.FromMinutes(5);
    private static readonly ILogger Logger = LogManager.GetLogger<BugReportService>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IBugReportLogSharingPreference logSharingPreference;
    private readonly ICoopLogSnapshotProvider logSnapshotProvider;
    private readonly IBugReportServerSaveProvider serverSaveProvider;
    private readonly IBugReportArchiveBuilder archiveBuilder;
    private readonly IBugReportLogValidator logValidator;
    private readonly IBugReportUploader uploader;
    private readonly CancellationToken cancellationToken;
    private readonly object collectionGate = new object();
    private readonly object clientRequestGate = new object();
    private readonly HashSet<string> clientRequests = new HashSet<string>(StringComparer.Ordinal);

    private ActiveCollection activeCollection;
    private DateTimeOffset nextCollectionAllowedAt;
    private int disposed;

    public BugReportService(
        IMessageBroker messageBroker,
        INetwork network,
        IPlayerManager playerManager,
        IBugReportLogSharingPreference logSharingPreference,
        ICoopLogSnapshotProvider logSnapshotProvider,
        IBugReportServerSaveProvider serverSaveProvider,
        IBugReportArchiveBuilder archiveBuilder,
        IBugReportLogValidator logValidator,
        IBugReportUploader uploader,
        CancellationTokenSource sessionCancellation)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.playerManager = playerManager;
        this.logSharingPreference = logSharingPreference;
        this.logSnapshotProvider = logSnapshotProvider;
        this.serverSaveProvider = serverSaveProvider;
        this.archiveBuilder = archiveBuilder;
        this.logValidator = logValidator;
        this.uploader = uploader;
        cancellationToken = sessionCancellation.Token;

        messageBroker.Subscribe<NetworkRequestBugReport>(Handle_NetworkRequestBugReport);
        messageBroker.Subscribe<NetworkRequestBugReportLogs>(Handle_NetworkRequestBugReportLogs);
        messageBroker.Subscribe<NetworkBugReportLogChunk>(Handle_NetworkBugReportLogChunk);
        messageBroker.Subscribe<NetworkBugReportLogUnavailable>(Handle_NetworkBugReportLogUnavailable);
        messageBroker.Subscribe<NetworkBugReportResult>(Handle_NetworkBugReportResult);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;

        messageBroker.Unsubscribe<NetworkRequestBugReport>(Handle_NetworkRequestBugReport);
        messageBroker.Unsubscribe<NetworkRequestBugReportLogs>(Handle_NetworkRequestBugReportLogs);
        messageBroker.Unsubscribe<NetworkBugReportLogChunk>(Handle_NetworkBugReportLogChunk);
        messageBroker.Unsubscribe<NetworkBugReportLogUnavailable>(Handle_NetworkBugReportLogUnavailable);
        messageBroker.Unsubscribe<NetworkBugReportResult>(Handle_NetworkBugReportResult);

        lock (collectionGate)
        {
            activeCollection?.Timer?.Dispose();
            activeCollection = null;
        }
    }

    public void RequestReport(
        string trigger,
        NetPeer requester,
        string summary = null,
        string description = null)
    {
        if (!ModInformation.IsServer) return;
        if (string.IsNullOrWhiteSpace(trigger))
            throw new ArgumentException("Trigger cannot be empty.", nameof(trigger));
        if (requester == null) throw new ArgumentNullException(nameof(requester));
        if (!playerManager.TryGetPlayer(requester, out var reportingPlayer))
            throw new InvalidOperationException("The reporting peer is not registered as a player.");

        var reportingClientNetworkId = reportingPlayer.ControllerId;
        BugReportSubmission submission = null;
        if (summary != null || description != null)
        {
            if (!TryNormalizeSubmission(summary, description, out summary, out description, out var error))
                throw new ArgumentException(error, nameof(summary));
            submission = new BugReportSubmission(
                reportingClientNetworkId,
                summary,
                description);
        }

        StartCollection(
            trigger.Trim(),
            requester,
            reportingClientNetworkId,
            submission);
    }

    public void SubmitReport(string summary, string description)
    {
        if (!ModInformation.IsClient) return;
        if (!TryNormalizeSubmission(summary, description, out summary, out description, out var error))
            throw new ArgumentException(error, nameof(summary));

        network.SendAll(new NetworkRequestBugReport(summary, description));
    }

    private void Handle_NetworkRequestBugReport(MessagePayload<NetworkRequestBugReport> payload)
    {
        if (!ModInformation.IsServer || !(payload.Who is NetPeer requester)) return;

        var request = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!playerManager.TryGetPlayer(requester, out _))
            {
                Logger.Warning("Ignoring a bug report from an unregistered peer");
                return;
            }

            if (!TryNormalizeSubmission(
                    request.Summary,
                    request.Description,
                    out var summary,
                    out var description,
                    out var error))
            {
                network.Send(requester, new NetworkBugReportResult(string.Empty, error));
                return;
            }

            RequestReport("player-submitted", requester, summary, description);
        }, context: nameof(BugReportService));
    }

    private static bool TryNormalizeSubmission(
        string inputSummary,
        string inputDescription,
        out string summary,
        out string description,
        out string error)
    {
        summary = (inputSummary ?? string.Empty)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();
        description = (inputDescription ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Trim();

        if (summary.Length == 0)
        {
            error = "Bug report summary is required.";
            return false;
        }
        if (summary.Length > NetworkRequestBugReport.MaximumSummaryLength)
        {
            error = $"Bug report summary cannot exceed {NetworkRequestBugReport.MaximumSummaryLength} characters.";
            return false;
        }
        if (description.Length == 0)
        {
            error = "Bug report description is required.";
            return false;
        }
        if (description.Length > NetworkRequestBugReport.MaximumDescriptionLength)
        {
            error = $"Bug report description cannot exceed {NetworkRequestBugReport.MaximumDescriptionLength} characters.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void StartCollection(
        string trigger,
        NetPeer requester,
        string reportingClientNetworkId,
        BugReportSubmission submission)
    {
        ActiveCollection collection;
        lock (collectionGate)
        {
            if (activeCollection != null)
            {
                network.Send(requester, new NetworkBugReportResult(
                    activeCollection.RequestId,
                    "A diagnostic bug report is already in progress."));
                return;
            }

            var now = DateTimeOffset.UtcNow;
            if (now < nextCollectionAllowedAt)
            {
                network.Send(requester, new NetworkBugReportResult(
                    string.Empty,
                    "A diagnostic bug report was recently started. Try again later."));
                return;
            }

            var peers = new List<NetPeer>();
            foreach (var player in playerManager.Players)
            {
                if (playerManager.TryGetPeer(player.ControllerId, out var peer) && !peers.Contains(peer))
                    peers.Add(peer);
            }

            if (peers.Count == 0)
            {
                network.Send(requester, new NetworkBugReportResult(
                    string.Empty,
                    "No connected player logs were available for the bug report."));
                return;
            }

            collection = new ActiveCollection(
                Guid.NewGuid().ToString("N"),
                trigger,
                reportingClientNetworkId,
                submission,
                peers,
                requester);
            activeCollection = collection;
            nextCollectionAllowedAt = now + MinimumCollectionInterval;
            collection.Timer = new Timer(
                _ => CompleteOnTimeout(collection.RequestId),
                null,
                CollectionTimeout,
                Timeout.InfiniteTimeSpan);
        }

        try
        {
            if (serverSaveProvider.TryCapture(out var serverSave))
                collection.ServerSave = serverSave;
        }
        catch (Exception exception)
        {
            Logger.Warning(exception, "Capturing the server campaign save for a bug report failed");
        }

        Logger.Information(
            "Starting diagnostic bug report {RequestId} for {ClientCount} connected clients",
            collection.RequestId,
            collection.Clients.Count);

        var request = new NetworkRequestBugReportLogs(collection.RequestId);
        foreach (var peer in collection.Clients.Keys)
        {
            try
            {
                network.Send(peer, request);
            }
            catch (Exception exception)
            {
                Logger.Warning(exception, "Could not request a diagnostic log from a client");
                RecordUnavailable(peer, collection.RequestId, BugReportLogUnavailableReason.CaptureFailed);
            }
        }
    }

    private void Handle_NetworkRequestBugReportLogs(
        MessagePayload<NetworkRequestBugReportLogs> payload)
    {
        if (!ModInformation.IsClient || string.IsNullOrEmpty(payload.What.RequestId)) return;

        if (!logSharingPreference.IsEnabled())
        {
            network.SendAll(new NetworkBugReportLogUnavailable(
                payload.What.RequestId,
                BugReportLogUnavailableReason.ConsentNotGranted));
            return;
        }

        lock (clientRequestGate)
        {
            if (!clientRequests.Add(payload.What.RequestId)) return;
        }

        _ = CaptureAndSendAsync(payload.What.RequestId);
    }

    private async Task CaptureAndSendAsync(string requestId)
    {
        CoopLogSnapshot snapshot = null;
        var reason = BugReportLogUnavailableReason.LogUnavailable;
        try
        {
            snapshot = await Task.Run(() =>
            {
                if (!logSnapshotProvider.TryCapture(out var captured)) return null;
                return captured;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            reason = BugReportLogUnavailableReason.CaptureFailed;
            Logger.Warning(exception, "Capturing the local co-op log for a bug report failed");
        }

        if (cancellationToken.IsCancellationRequested || Volatile.Read(ref disposed) != 0) return;

        try
        {
            if (snapshot == null)
            {
                SendOnGameThread(new NetworkBugReportLogUnavailable(requestId, reason));
                return;
            }

            SendSnapshotPaced(requestId, snapshot);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Logger.Warning(exception, "Sending the local co-op log for a bug report failed");
        }
        finally
        {
            lock (clientRequestGate)
            {
                clientRequests.Remove(requestId);
            }
        }
    }

    private void SendSnapshotPaced(string requestId, CoopLogSnapshot snapshot)
    {
        var compressed = snapshot.CompressedData;
        if (compressed == null || compressed.Length == 0 || compressed.Length > MaximumCompressedLogBytes)
        {
            SendOnGameThread(new NetworkBugReportLogUnavailable(
                requestId,
                BugReportLogUnavailableReason.CaptureFailed));
            return;
        }

        var chunkCount = (compressed.Length + NetworkBugReportLogChunk.ChunkSize - 1) /
                         NetworkBugReportLogChunk.ChunkSize;
        for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            if (cancellationToken.IsCancellationRequested || Volatile.Read(ref disposed) != 0)
                return;

            var offset = chunkIndex * NetworkBugReportLogChunk.ChunkSize;
            var length = Math.Min(NetworkBugReportLogChunk.ChunkSize, compressed.Length - offset);
            var data = new byte[length];
            Buffer.BlockCopy(compressed, offset, data, 0, length);
            var chunk = new NetworkBugReportLogChunk(
                requestId,
                chunkIndex,
                chunkCount,
                compressed.Length,
                snapshot.UncompressedLength,
                data);
            SendOnGameThread(chunk);
        }
    }

    private void SendOnGameThread<T>(T message) where T : IMessage
    {
        GameThread.Run(
            () => network.SendAll(message),
            blocking: true,
            label: nameof(BugReportService));
    }

    private void Handle_NetworkBugReportLogChunk(
        MessagePayload<NetworkBugReportLogChunk> payload)
    {
        if (!ModInformation.IsServer || !(payload.Who is NetPeer peer)) return;

        FinalizedCollection finalized = null;
        lock (collectionGate)
        {
            var collection = activeCollection;
            if (collection == null || collection.RequestId != payload.What.RequestId ||
                !collection.Clients.TryGetValue(peer, out var client) || client.Responded)
            {
                return;
            }

            if (!TryAppendChunk(collection, client, payload.What))
            {
                ReleaseClientBuffer(collection, client);
                client.Responded = true;
                client.Status = ClientCollectionStatus.Failed;
            }

            if (collection.Clients.Values.All(item => item.Responded))
                finalized = ClaimFinalization(collection, timedOut: false);
        }

        if (finalized != null) _ = PackageAndUploadAsync(finalized);
    }

    private static bool TryAppendChunk(
        ActiveCollection collection,
        ClientCollection client,
        NetworkBugReportLogChunk chunk)
    {
        var maximumChunks = (MaximumCompressedLogBytes + NetworkBugReportLogChunk.ChunkSize - 1) /
                            NetworkBugReportLogChunk.ChunkSize;
        if (chunk.ChunkCount <= 0 || chunk.ChunkCount > maximumChunks ||
            chunk.ChunkIndex != client.NextChunkIndex ||
            chunk.ChunkIndex >= chunk.ChunkCount ||
            chunk.CompressedLength <= 0 || chunk.CompressedLength > MaximumCompressedLogBytes ||
            chunk.UncompressedLength < 0 || chunk.UncompressedLength > CoopLogSnapshotProvider.MaximumLogBytes ||
            chunk.Data == null || chunk.Data.Length == 0 ||
            chunk.Data.Length > NetworkBugReportLogChunk.ChunkSize)
        {
            return false;
        }

        if (client.NextChunkIndex == 0)
        {
            var expectedChunks = (chunk.CompressedLength + NetworkBugReportLogChunk.ChunkSize - 1) /
                                 NetworkBugReportLogChunk.ChunkSize;
            if (chunk.ChunkCount != expectedChunks ||
                !CanReserveCompressedBytes(collection.BufferedBytes, chunk.CompressedLength))
            {
                return false;
            }

            client.ChunkCount = chunk.ChunkCount;
            client.CompressedLength = chunk.CompressedLength;
            client.UncompressedLength = chunk.UncompressedLength;
            client.Data = new byte[chunk.CompressedLength];
            collection.BufferedBytes += chunk.CompressedLength;
        }
        else if (client.ChunkCount != chunk.ChunkCount ||
                 client.CompressedLength != chunk.CompressedLength ||
                 client.UncompressedLength != chunk.UncompressedLength)
        {
            return false;
        }

        if (client.Data == null ||
            client.BytesWritten + chunk.Data.Length > client.CompressedLength)
        {
            return false;
        }

        Buffer.BlockCopy(chunk.Data, 0, client.Data, client.BytesWritten, chunk.Data.Length);
        client.BytesWritten += chunk.Data.Length;
        client.NextChunkIndex++;
        if (client.NextChunkIndex != client.ChunkCount) return true;
        if (client.BytesWritten != client.CompressedLength) return false;

        client.Responded = true;
        client.Status = ClientCollectionStatus.Collected;
        return true;
    }

    internal static bool CanReserveCompressedBytes(long bufferedBytes, int requestedBytes)
    {
        return bufferedBytes >= 0 && requestedBytes > 0 &&
               bufferedBytes + requestedBytes <= MaximumCombinedCompressedBytes;
    }

    private static void ReleaseClientBuffer(
        ActiveCollection collection,
        ClientCollection client)
    {
        if (client.Data == null) return;

        collection.BufferedBytes -= client.CompressedLength;
        client.Data = null;
        client.BytesWritten = 0;
    }

    private void Handle_NetworkBugReportLogUnavailable(
        MessagePayload<NetworkBugReportLogUnavailable> payload)
    {
        if (!ModInformation.IsServer || !(payload.Who is NetPeer peer)) return;
        RecordUnavailable(peer, payload.What.RequestId, payload.What.Reason);
    }

    private void RecordUnavailable(
        NetPeer peer,
        string requestId,
        BugReportLogUnavailableReason reason)
    {
        FinalizedCollection finalized = null;
        lock (collectionGate)
        {
            var collection = activeCollection;
            if (collection == null || collection.RequestId != requestId ||
                !collection.Clients.TryGetValue(peer, out var client) || client.Responded)
            {
                return;
            }

            ReleaseClientBuffer(collection, client);
            client.Responded = true;
            client.Status = reason == BugReportLogUnavailableReason.ConsentNotGranted
                ? ClientCollectionStatus.Declined
                : ClientCollectionStatus.Failed;

            if (collection.Clients.Values.All(item => item.Responded))
                finalized = ClaimFinalization(collection, timedOut: false);
        }

        if (finalized != null) _ = PackageAndUploadAsync(finalized);
    }

    private void CompleteOnTimeout(string requestId)
    {
        FinalizedCollection finalized = null;
        lock (collectionGate)
        {
            if (activeCollection != null && activeCollection.RequestId == requestId)
                finalized = ClaimFinalization(activeCollection, timedOut: true);
        }

        if (finalized != null) _ = PackageAndUploadAsync(finalized);
    }

    private FinalizedCollection ClaimFinalization(ActiveCollection collection, bool timedOut)
    {
        if (!ReferenceEquals(activeCollection, collection) || collection.Finalizing) return null;

        collection.Finalizing = true;
        collection.Timer?.Dispose();

        var logs = new List<CollectedBugReportLog>();
        foreach (var client in collection.Clients.Values)
        {
            if (client.Status == ClientCollectionStatus.Collected)
            {
                logs.Add(new CollectedBugReportLog(
                    client.ClientNumber,
                    client.Data,
                    client.UncompressedLength));
                client.Data = null;
            }
            else
            {
                ReleaseClientBuffer(collection, client);
            }
        }
        collection.BufferedBytes = 0;

        var timedOutClients = timedOut
            ? collection.Clients.Values.Count(client => !client.Responded)
            : 0;

        return new FinalizedCollection(
            new BugReportArchiveContents(
                collection.RequestId,
                collection.ReportingClientNetworkId,
                collection.Triggers.OrderBy(trigger => trigger).ToArray(),
                collection.Submissions.ToArray(),
                null,
                collection.ServerSave,
                collection.StartedAt,
                logs,
                collection.Clients.Count,
                collection.Clients.Values.Count(client => client.Status == ClientCollectionStatus.Declined),
                collection.Clients.Values.Count(client => client.Status == ClientCollectionStatus.Failed),
                timedOutClients),
            collection.Requesters.ToArray());
    }

    private async Task PackageAndUploadAsync(FinalizedCollection finalized)
    {
        try
        {
            await PackageAndUploadCoreAsync(finalized).ConfigureAwait(false);
        }
        finally
        {
            lock (collectionGate)
            {
                if (activeCollection?.RequestId == finalized.Contents.RequestId)
                    activeCollection = null;
            }
        }
    }

    private async Task PackageAndUploadCoreAsync(FinalizedCollection finalized)
    {
        BugReportArchiveContents contents;
        try
        {
            contents = await Task.Run(
                () => PrepareContents(finalized.Contents),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Preparing the diagnostic bug report failed");
            SendResult(finalized, "The server could not prepare the bug report.");
            return;
        }

        if (contents.ServerLog == null &&
            contents.ServerSave == null &&
            contents.Logs.Count == 0 &&
            contents.Submissions.Count == 0)
        {
            SendResult(finalized, "No diagnostic logs were available for the bug report.");
            return;
        }

        BugReportUploadResult upload;
        try
        {
            upload = await uploader.UploadAsync(contents, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            Logger.Warning(exception, "Uploading diagnostic bug report {RequestId} failed", contents.RequestId);
            upload = new BugReportUploadResult(false, true, exception.Message);
        }

        if (upload.Uploaded)
        {
            Logger.Information("Uploaded diagnostic bug report {RequestId}", contents.RequestId);
            SendResult(
                finalized,
                contents.ServerSave != null
                    ? "The bug report, server save, and available diagnostic logs were submitted."
                    : "The bug report and available diagnostic logs were submitted, but the server save could not be created.");
            return;
        }

        string archivePath;
        try
        {
            archivePath = await Task.Run(
                () => archiveBuilder.Create(contents),
                cancellationToken).ConfigureAwait(false);
            Logger.Information(
                "Created diagnostic bug-report fallback archive {RequestId} at {ArchivePath}",
                contents.RequestId,
                archivePath);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Saving the diagnostic bug-report fallback failed");
            SendResult(finalized, upload.EndpointConfigured
                ? "The bug-report upload failed and its local fallback could not be saved."
                : "Bug-report uploads are not configured and the local fallback could not be saved.");
            return;
        }

        if (!upload.EndpointConfigured)
        {
            Logger.Information(
                "Diagnostic bug report {RequestId} was not uploaded because the endpoint is not configured",
                contents.RequestId);
            SendResult(finalized, "The bug report was saved on the server; the upload endpoint is not configured.");
        }
        else
        {
            Logger.Warning(
                "Uploading diagnostic bug report {RequestId} failed: {Details}",
                contents.RequestId,
                upload.Details);
            SendResult(finalized, "The bug-report upload failed, so it was saved on the server.");
        }
    }

    internal BugReportArchiveContents PrepareContents(BugReportArchiveContents contents)
    {
        return CaptureServerLog(ValidateClientLogs(contents));
    }

    private BugReportArchiveContents ValidateClientLogs(BugReportArchiveContents contents)
    {
        var validation = logValidator.Validate(contents.Logs);
        if (validation.InvalidCount == 0) return contents;

        Logger.Warning(
            "Rejected {InvalidCount} invalid client log(s) from diagnostic bug report {RequestId}",
            validation.InvalidCount,
            contents.RequestId);
        return contents.WithValidatedLogs(validation.ValidLogs, validation.InvalidCount);
    }

    private BugReportArchiveContents CaptureServerLog(BugReportArchiveContents contents)
    {
        try
        {
            if (!logSnapshotProvider.TryCapture(out var snapshot)) return contents;

            return contents.WithServerLog(new CollectedBugReportServerLog(
                snapshot.CompressedData,
                snapshot.UncompressedLength));
        }
        catch (Exception exception)
        {
            Logger.Warning(exception, "Capturing the server log for a bug report failed");
            return contents;
        }
    }

    private void SendResult(FinalizedCollection finalized, string message)
    {
        if (cancellationToken.IsCancellationRequested || Volatile.Read(ref disposed) != 0) return;

        GameThread.RunSafe(() =>
        {
            if (cancellationToken.IsCancellationRequested || Volatile.Read(ref disposed) != 0) return;

            var result = new NetworkBugReportResult(finalized.Contents.RequestId, message);
            foreach (var requester in finalized.Requesters)
            {
                network.Send(requester, result);
            }
        }, context: nameof(BugReportService));
    }

    private void Handle_NetworkBugReportResult(
        MessagePayload<NetworkBugReportResult> payload)
    {
        if (!ModInformation.IsClient || string.IsNullOrWhiteSpace(payload.What.Message)) return;

        var message = payload.What.Message;
        GameThread.RunSafe(
            () => InformationManager.DisplayMessage(new InformationMessage("[Bug Report] " + message)),
            context: nameof(BugReportService));
    }

    private enum ClientCollectionStatus
    {
        Pending,
        Collected,
        Declined,
        Failed,
    }

    private sealed class ClientCollection
    {
        public int ClientNumber { get; }
        public byte[] Data { get; set; }
        public int BytesWritten { get; set; }
        public int NextChunkIndex { get; set; }
        public int ChunkCount { get; set; }
        public int CompressedLength { get; set; }
        public int UncompressedLength { get; set; }
        public bool Responded { get; set; }
        public ClientCollectionStatus Status { get; set; }

        public ClientCollection(int clientNumber)
        {
            ClientNumber = clientNumber;
        }
    }

    private sealed class ActiveCollection
    {
        public string RequestId { get; }
        public string ReportingClientNetworkId { get; }
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
        public HashSet<string> Triggers { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public List<BugReportSubmission> Submissions { get; } = new List<BugReportSubmission>();
        public Dictionary<NetPeer, ClientCollection> Clients { get; }
        public HashSet<NetPeer> Requesters { get; } = new HashSet<NetPeer>();
        public Timer Timer { get; set; }
        public CollectedBugReportServerSave ServerSave { get; set; }
        public bool Finalizing { get; set; }
        public long BufferedBytes { get; set; }

        public ActiveCollection(
            string requestId,
            string trigger,
            string reportingClientNetworkId,
            BugReportSubmission submission,
            IEnumerable<NetPeer> peers,
            NetPeer requester)
        {
            RequestId = requestId;
            ReportingClientNetworkId = reportingClientNetworkId;
            Triggers.Add(trigger);
            if (submission != null) Submissions.Add(submission);
            Clients = peers
                .Select((peer, index) => new { peer, client = new ClientCollection(index + 1) })
                .ToDictionary(item => item.peer, item => item.client);
            Requesters.Add(requester);
        }
    }

    private sealed class FinalizedCollection
    {
        public BugReportArchiveContents Contents { get; }
        public NetPeer[] Requesters { get; }

        public FinalizedCollection(BugReportArchiveContents contents, NetPeer[] requesters)
        {
            Contents = contents;
            Requesters = requesters;
        }
    }
}
