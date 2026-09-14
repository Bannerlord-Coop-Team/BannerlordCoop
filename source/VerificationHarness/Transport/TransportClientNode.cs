using LiteNetLib;

namespace VerificationHarness.Transport;

internal sealed class TransportClientNode : TransportNodeBase
{
    private NetPeer? serverPeer;
    private int generation = 1;
    private long nextOutgoingSequence = 1;
    private long expectedIncomingSequence = 1;
    private bool reconnectPending;
    private bool awaitingReconnectDisconnect;
    private bool finished;

    public TransportClientNode(TransportNodeOptions options)
        : base(options)
    {
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
        if (!manager.Start())
        {
            Result.Error = "Could not start LiteNetLib client.";
            return Result;
        }

        Connect();
        DateTime deadlineUtc = DateTime.UtcNow.AddMilliseconds(Options.TimeoutMilliseconds);
        try
        {
            while (!cancellationToken.IsCancellationRequested && DateTime.UtcNow < deadlineUtc)
            {
                manager.PollEvents();
                if (reconnectPending && serverPeer == null)
                {
                    reconnectPending = false;
                    generation++;
                    nextOutgoingSequence = 1;
                    expectedIncomingSequence = 1;
                    Result.HighestGeneration = generation;
                    Connect();
                }

                if (finished)
                {
                    Result.Success = Result.Error == null && IsExpectedOutcomeComplete();
                    if (!Result.Success && Result.Error == null)
                    {
                        Result.Error = "The expected client transport outcome was incomplete.";
                    }

                    return Result;
                }

                await Task.Delay(1, cancellationToken);
            }

            Result.Error = cancellationToken.IsCancellationRequested
                ? "Transport client was cancelled."
                : "Transport client timed out.";
            return Result;
        }
        finally
        {
            manager.Stop();
        }
    }

    public override void OnConnectionRequest(ConnectionRequest request)
    {
        request.Reject();
    }

    public override void OnPeerConnected(NetPeer peer)
    {
        serverPeer = peer;
        if (Options.Scenario == TransportScenarios.Timeout) return;

        if (Options.InstanceId == "client-b" && Options.Scenario == TransportScenarios.Malformed)
        {
            byte[] malformed = { 0xFF, 0x00, 0xFE, 0x01 };
            RecordRawSent(malformed, "Malformed");
            peer.Send(malformed, DeliveryMethod.ReliableOrdered);
            return;
        }

        long sequence = Options.InstanceId == "client-b" &&
                        Options.Scenario == TransportScenarios.OutOfSequence
            ? 2
            : nextOutgoingSequence++;
        Send(
            peer,
            Options.InstanceId,
            generation,
            sequence,
            TransportMessageKind.Hello,
            new TransportHelloPayload { Role = "client" });
    }

    public override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        serverPeer = null;
        if (reconnectPending || finished) return;

        if (Options.Scenario == TransportScenarios.Reconnect &&
            Options.InstanceId == "client-b" &&
            generation == 1 &&
            awaitingReconnectDisconnect)
        {
            awaitingReconnectDisconnect = false;
            reconnectPending = true;
            return;
        }

        Result.Error = $"Unexpected disconnect: {disconnectInfo.Reason}.";
        finished = true;
    }

    public override void OnNetworkReceive(
        NetPeer peer,
        NetPacketReader reader,
        byte channelNumber,
        DeliveryMethod deliveryMethod)
    {
        RecordDeliveryDomain(channelNumber, deliveryMethod);
        TransportDecodedFrame frame;
        try
        {
            frame = Codec.Decode(reader.GetRemainingBytes());
            RecordReceived(frame);
        }
        catch (Exception ex)
        {
            Result.Error = $"Could not decode server frame: {ex.GetType().Name}.";
            finished = true;
            return;
        }

        TransportEnvelope envelope = frame.Envelope;
        if (envelope.ProtocolVersion != TransportCodec.CurrentProtocolVersion ||
            !string.Equals(envelope.InstanceId, "server", StringComparison.Ordinal) ||
            envelope.Generation != generation)
        {
            Result.Error = "Server envelope identity or generation was invalid.";
            finished = true;
            return;
        }

        if (envelope.Sequence != expectedIncomingSequence)
        {
            Result.Error = $"Expected server sequence {expectedIncomingSequence}, received {envelope.Sequence}.";
            finished = true;
            return;
        }

        expectedIncomingSequence++;
        switch (envelope.Kind)
        {
            case TransportMessageKind.State:
                HandleState(peer, (TransportStatePayload)frame.Payload);
                break;

            case TransportMessageKind.Rejection:
                var rejection = (TransportRejectionPayload)frame.Payload;
                Result.RejectionCode = rejection.Code;
                finished = true;
                break;

            case TransportMessageKind.Shutdown:
                SendGoodbye(peer, "shutdown");
                break;

            case TransportMessageKind.ShutdownAcknowledged:
                serverPeer?.Disconnect();
                finished = true;
                break;

            default:
                Result.Error = $"Unexpected server message kind: {envelope.Kind}.";
                finished = true;
                break;
        }
    }

    private void HandleState(NetPeer peer, TransportStatePayload state)
    {
        Result.LocalState = new TransportStateSnapshot(
            TransportCodec.CurrentProtocolVersion,
            state.StateVersion,
            state.Marker);
        Result.LocalDigest = Codec.ComputeStateDigest(state);
        Result.ObservedDigests[Options.InstanceId] = Result.LocalDigest;

        if (Options.Scenario == TransportScenarios.Reconnect &&
            Options.InstanceId == "client-b" &&
            generation == 1)
        {
            SendGoodbye(peer, "reconnect");
            awaitingReconnectDisconnect = true;
            return;
        }

        string acknowledgementDigest = Options.Scenario == TransportScenarios.CorruptAcknowledgement &&
                                       Options.InstanceId == "client-b"
            ? new string('0', 64)
            : Result.LocalDigest;

        Send(
            peer,
            Options.InstanceId,
            generation,
            nextOutgoingSequence++,
            TransportMessageKind.Acknowledgement,
            new TransportAcknowledgementPayload { Digest = acknowledgementDigest });
    }

    private void SendGoodbye(NetPeer peer, string reason)
    {
        Send(
            peer,
            Options.InstanceId,
            generation,
            nextOutgoingSequence++,
            TransportMessageKind.Goodbye,
            new TransportGoodbyePayload { Reason = reason });
    }

    private void Connect()
    {
        if (Manager == null) throw new InvalidOperationException("Client manager has not started.");
        Manager.Connect("127.0.0.1", Options.Port, ConnectionTokenPrefix + Options.InstanceId);
    }

    private bool IsExpectedOutcomeComplete()
    {
        if (Options.InstanceId == "client-b" && TransportScenarios.IsNegativeProtocolCase(Options.Scenario))
        {
            string expected = TransportScenarios.ExpectedRejectionCode(Options.Scenario)!;
            return string.Equals(Result.RejectionCode, expected, StringComparison.Ordinal);
        }

        if (Options.Scenario == TransportScenarios.Reconnect && Options.InstanceId == "client-b")
        {
            return generation == 2 && Result.LocalDigest != null;
        }

        if (TransportScenarios.IsNegativeProtocolCase(Options.Scenario))
        {
            return true;
        }

        return Result.LocalDigest != null;
    }
}
