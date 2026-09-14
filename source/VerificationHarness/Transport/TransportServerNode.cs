using LiteNetLib;
using System.Text.Json;

namespace VerificationHarness.Transport;

internal sealed class TransportServerNode : TransportNodeBase
{
    private readonly TextWriter output;
    private readonly Dictionary<int, string> expectedInstanceByPeer = new();
    private readonly Dictionary<int, ActiveClient> activeClients = new();
    private readonly Dictionary<string, int> highestGeneration = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> expectedIncomingSequence = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> nextOutgoingSequence = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> expectedAcknowledgementDigest = new(StringComparer.Ordinal);
    private readonly HashSet<string> stateSent = new(StringComparer.Ordinal);
    private readonly HashSet<string> cleanGoodbyes = new(StringComparer.Ordinal);
    private readonly HashSet<string> shutdownExpected = new(StringComparer.Ordinal);
    private readonly HashSet<string> shutdownDisconnected = new(StringComparer.Ordinal);

    private bool finishing;
    private bool finished;
    private bool protocolFailure;
    private bool expectedNegativeRejectionObserved;

    public TransportServerNode(TransportNodeOptions options, TextWriter output)
        : base(options)
    {
        this.output = output;
        TransportStatePayload state = CreateState("synchronized");
        Result.LocalState = new TransportStateSnapshot(
            TransportCodec.CurrentProtocolVersion,
            state.StateVersion,
            state.Marker);
        Result.LocalDigest = Codec.ComputeStateDigest(state);
        Result.ObservedDigests["server"] = Result.LocalDigest;
    }

    public async Task<TransportNodeResult> RunAsync(CancellationToken cancellationToken)
    {
        var manager = new NetManager(this)
        {
            DisconnectTimeout = 1000,
            UpdateTime = 1,
            ChannelsCount = 1
        };
        Manager = manager;

        if (!manager.Start(Options.Port))
        {
            Result.Error = $"Could not bind LiteNetLib server to UDP port {Options.Port}.";
            return Result;
        }

        await output.WriteLineAsync(JsonSerializer.Serialize(
            new TransportReadyEvent
            {
                ProcessId = Environment.ProcessId,
                Port = manager.LocalPort
            },
            TransportJson.Options));
        await output.FlushAsync();

        DateTime deadlineUtc = DateTime.UtcNow.AddMilliseconds(Options.TimeoutMilliseconds);
        try
        {
            while (!cancellationToken.IsCancellationRequested && DateTime.UtcNow < deadlineUtc)
            {
                manager.PollEvents();
                if (finished)
                {
                    Result.Success = !protocolFailure && IsExpectedOutcomeComplete();
                    if (!Result.Success && Result.Error == null)
                    {
                        Result.Error = "The expected transport outcome was incomplete.";
                    }

                    return Result;
                }

                await Task.Delay(1, cancellationToken);
            }

            Result.Error = cancellationToken.IsCancellationRequested
                ? "Transport server was cancelled."
                : "Transport server timed out.";
            return Result;
        }
        finally
        {
            manager.DisconnectAll();
            manager.Stop();
        }
    }

    public override void OnConnectionRequest(ConnectionRequest request)
    {
        string token;
        try
        {
            token = request.Data.GetString(128);
        }
        catch (Exception)
        {
            request.Reject();
            return;
        }

        if (!token.StartsWith(ConnectionTokenPrefix, StringComparison.Ordinal))
        {
            request.Reject();
            return;
        }

        string instanceId = token.Substring(ConnectionTokenPrefix.Length);
        if (instanceId is not "client-a" and not "client-b")
        {
            request.Reject();
            return;
        }

        NetPeer peer = request.Accept();
        expectedInstanceByPeer[peer.Id] = instanceId;
    }

    public override void OnPeerConnected(NetPeer peer)
    {
    }

    public override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        string? instanceId = activeClients.TryGetValue(peer.Id, out ActiveClient? active)
            ? active.InstanceId
            : expectedInstanceByPeer.GetValueOrDefault(peer.Id);
        activeClients.Remove(peer.Id);
        expectedInstanceByPeer.Remove(peer.Id);
        if (instanceId != null && shutdownExpected.Contains(instanceId))
        {
            shutdownDisconnected.Add(instanceId);
            if (shutdownDisconnected.SetEquals(shutdownExpected)) finished = true;
        }
    }

    public override void OnNetworkReceive(
        NetPeer peer,
        NetPacketReader reader,
        byte channelNumber,
        DeliveryMethod deliveryMethod)
    {
        RecordDeliveryDomain(channelNumber, deliveryMethod);
        byte[] wireBytes = reader.GetRemainingBytes();
        TransportDecodedFrame frame;
        try
        {
            frame = Codec.Decode(wireBytes);
            RecordReceived(frame);
        }
        catch (Exception ex)
        {
            RecordMalformed(wireBytes);
            Reject(peer, "malformed-frame", ex.GetType().Name);
            return;
        }

        TransportEnvelope envelope = frame.Envelope;
        if (envelope.ProtocolVersion != TransportCodec.CurrentProtocolVersion)
        {
            Reject(peer, "unsupported-protocol", $"Expected {TransportCodec.CurrentProtocolVersion}.");
            return;
        }

        if (!expectedInstanceByPeer.TryGetValue(peer.Id, out string? expectedInstance) ||
            !string.Equals(envelope.InstanceId, expectedInstance, StringComparison.Ordinal))
        {
            Reject(peer, "instance-mismatch", "Envelope instance id did not match the connection token.");
            return;
        }

        if (envelope.Kind == TransportMessageKind.Hello)
        {
            HandleHello(peer, frame);
            return;
        }

        if (!activeClients.TryGetValue(peer.Id, out ActiveClient? active) ||
            envelope.Generation != active.Generation)
        {
            Reject(peer, "invalid-generation", "A hello handshake is required for this connection generation.");
            return;
        }

        string streamKey = StreamKey(envelope.InstanceId, envelope.Generation);
        if (!ValidateAndAdvanceSequence(streamKey, envelope.Sequence, peer)) return;

        switch (envelope.Kind)
        {
            case TransportMessageKind.Acknowledgement:
                var acknowledgement = (TransportAcknowledgementPayload)frame.Payload;
                if (!expectedAcknowledgementDigest.TryGetValue(streamKey, out string? expectedDigest) ||
                    !string.Equals(acknowledgement.Digest, expectedDigest, StringComparison.Ordinal))
                {
                    Reject(peer, "digest-mismatch", "Acknowledgement did not match the state sent on this connection generation.");
                    return;
                }

                Result.ObservedDigests[envelope.InstanceId] = acknowledgement.Digest;
                TryCompletePositiveScenario();
                break;

            case TransportMessageKind.Goodbye:
                cleanGoodbyes.Add(streamKey);
                if (finishing && shutdownExpected.Contains(envelope.InstanceId))
                {
                    SendToClient(
                        active,
                        TransportMessageKind.ShutdownAcknowledged,
                        new TransportShutdownAcknowledgedPayload { Reason = "goodbye-received" });
                }
                else if (Options.Scenario == TransportScenarios.Reconnect &&
                    envelope.InstanceId == "client-b" &&
                    envelope.Generation == 1)
                {
                    peer.Disconnect();
                }
                break;

            default:
                Reject(peer, "unexpected-kind", $"Server cannot receive {envelope.Kind}.");
                break;
        }
    }

    private void HandleHello(NetPeer peer, TransportDecodedFrame frame)
    {
        TransportEnvelope envelope = frame.Envelope;
        int priorGeneration = highestGeneration.GetValueOrDefault(envelope.InstanceId);
        int requiredGeneration = priorGeneration == 0 ? 1 : priorGeneration + 1;
        bool reconnectAllowed = Options.Scenario == TransportScenarios.Reconnect &&
                                envelope.InstanceId == "client-b" &&
                                priorGeneration == 1 &&
                                cleanGoodbyes.Contains(StreamKey(envelope.InstanceId, priorGeneration));

        if (envelope.Generation != requiredGeneration || (priorGeneration > 0 && !reconnectAllowed))
        {
            Reject(peer, "invalid-generation", $"Expected generation {requiredGeneration}.");
            return;
        }

        string streamKey = StreamKey(envelope.InstanceId, envelope.Generation);
        if (!ValidateAndAdvanceSequence(streamKey, envelope.Sequence, peer)) return;

        var hello = (TransportHelloPayload)frame.Payload;
        if (!string.Equals(hello.Role, "client", StringComparison.Ordinal))
        {
            Reject(peer, "invalid-role", "Hello role must be client.");
            return;
        }

        activeClients[peer.Id] = new ActiveClient(envelope.InstanceId, envelope.Generation, peer);
        highestGeneration[envelope.InstanceId] = envelope.Generation;
        Result.HighestGeneration = Math.Max(Result.HighestGeneration, envelope.Generation);
        if (envelope.InstanceId == "client-b" && envelope.Generation == 2)
        {
            Result.CleanReconnectObserved = reconnectAllowed;
        }

        TrySendState();
        TryBeginNegativeShutdown();
    }

    private bool ValidateAndAdvanceSequence(string streamKey, long sequence, NetPeer peer)
    {
        long expected = expectedIncomingSequence.GetValueOrDefault(streamKey, 1);
        if (sequence != expected)
        {
            Reject(peer, "invalid-sequence", $"Expected sequence {expected}.");
            return false;
        }

        expectedIncomingSequence[streamKey] = expected + 1;
        return true;
    }

    private void TrySendState()
    {
        if (Options.Scenario == TransportScenarios.Timeout ||
            TransportScenarios.IsPreStateNegativeProtocolCase(Options.Scenario))
        {
            return;
        }

        ActiveClient? clientA = activeClients.Values.FirstOrDefault(x => x.InstanceId == "client-a");
        ActiveClient? clientB = activeClients.Values.FirstOrDefault(x => x.InstanceId == "client-b");
        if (clientA == null || clientB == null) return;

        SendStateIfNeeded(clientA);
        SendStateIfNeeded(clientB);
    }

    private void SendStateIfNeeded(ActiveClient client)
    {
        string streamKey = StreamKey(client.InstanceId, client.Generation);
        if (!stateSent.Add(streamKey)) return;

        string marker = Options.Scenario == TransportScenarios.Diverge && client.InstanceId == "client-b"
            ? "intentional-divergence"
            : "synchronized";
        TransportStatePayload state = CreateState(marker);
        expectedAcknowledgementDigest[streamKey] = Codec.ComputeStateDigest(state);
        SendToClient(
            client,
            TransportMessageKind.State,
            state);
    }

    private void TryCompletePositiveScenario()
    {
        if (Options.Scenario == TransportScenarios.Reconnect)
        {
            if (Result.ObservedDigests.ContainsKey("client-a") &&
                highestGeneration.GetValueOrDefault("client-b") == 2 &&
                Result.ObservedDigests.ContainsKey("client-b") &&
                Result.CleanReconnectObserved)
            {
                BeginShutdown();
            }

            return;
        }

        if (Result.ObservedDigests.ContainsKey("client-a") &&
            Result.ObservedDigests.ContainsKey("client-b"))
        {
            BeginShutdown();
        }
    }

    private void Reject(NetPeer peer, string code, string detail)
    {
        string instanceId = expectedInstanceByPeer.GetValueOrDefault(peer.Id, "unknown");
        int generation = activeClients.TryGetValue(peer.Id, out ActiveClient? active)
            ? active.Generation
            : Math.Max(1, highestGeneration.GetValueOrDefault(instanceId, 1));
        Send(
            peer,
            "server",
            generation,
            NextOutgoingSequence(instanceId, generation),
            TransportMessageKind.Rejection,
            new TransportRejectionPayload { Code = code, Detail = detail });
        Result.RejectionCode = code;

        string? expectedCode = TransportScenarios.ExpectedRejectionCode(Options.Scenario);
        if (instanceId == "client-b" && string.Equals(code, expectedCode, StringComparison.Ordinal))
        {
            expectedNegativeRejectionObserved = true;
            TryBeginNegativeShutdown();
            return;
        }

        protocolFailure = true;
        Result.Error = $"Unexpected protocol rejection for {instanceId}: {code}.";
        BeginShutdown();
    }

    private void TryBeginNegativeShutdown()
    {
        if (!expectedNegativeRejectionObserved) return;
        if (!activeClients.Values.Any(x => x.InstanceId == "client-a")) return;
        BeginShutdown(excludedInstanceId: "client-b");
    }

    private void BeginShutdown(string? excludedInstanceId = null)
    {
        if (finishing) return;
        finishing = true;

        foreach (ActiveClient client in activeClients.Values.ToArray())
        {
            if (string.Equals(client.InstanceId, excludedInstanceId, StringComparison.Ordinal)) continue;
            shutdownExpected.Add(client.InstanceId);
            SendToClient(
                client,
                TransportMessageKind.Shutdown,
                new TransportShutdownPayload { Reason = "scenario-complete" });
        }

        if (shutdownExpected.Count == 0) finished = true;
    }

    private void SendToClient<TPayload>(ActiveClient client, TransportMessageKind kind, TPayload payload)
    {
        Send(
            client.Peer,
            "server",
            client.Generation,
            NextOutgoingSequence(client.InstanceId, client.Generation),
            kind,
            payload);
    }

    private long NextOutgoingSequence(string instanceId, int generation)
    {
        string key = StreamKey(instanceId, generation);
        long sequence = nextOutgoingSequence.GetValueOrDefault(key, 1);
        nextOutgoingSequence[key] = sequence + 1;
        return sequence;
    }

    private bool IsExpectedOutcomeComplete()
    {
        if (TransportScenarios.IsNegativeProtocolCase(Options.Scenario))
        {
            string expected = TransportScenarios.ExpectedRejectionCode(Options.Scenario)!;
            return string.Equals(Result.RejectionCode, expected, StringComparison.Ordinal);
        }

        if (Options.Scenario == TransportScenarios.Reconnect)
        {
            return Result.CleanReconnectObserved &&
                   highestGeneration.GetValueOrDefault("client-b") == 2 &&
                   Result.ObservedDigests.ContainsKey("client-a") &&
                   Result.ObservedDigests.ContainsKey("client-b");
        }

        return Result.ObservedDigests.ContainsKey("client-a") &&
               Result.ObservedDigests.ContainsKey("client-b");
    }

    private static string StreamKey(string instanceId, int generation)
    {
        return $"{instanceId}:{generation}";
    }

    private TransportStatePayload CreateState(string marker) => new()
    {
        StateVersion = 1,
        Marker = $"{marker}:{Options.Seed}"
    };

    private sealed class ActiveClient
    {
        public string InstanceId { get; }
        public int Generation { get; }
        public NetPeer Peer { get; }

        public ActiveClient(string instanceId, int generation, NetPeer peer)
        {
            InstanceId = instanceId;
            Generation = generation;
            Peer = peer;
        }
    }
}
