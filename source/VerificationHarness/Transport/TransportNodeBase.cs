using LiteNetLib;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace VerificationHarness.Transport;

internal abstract class TransportNodeBase : INetEventListener
{
    protected const string ConnectionTokenPrefix = "process-peer-v1:";

    protected readonly TransportNodeOptions Options;
    protected readonly ITransportCodec Codec;
    protected readonly TransportNodeResult Result;
    protected NetManager? Manager;

    protected TransportNodeBase(TransportNodeOptions options)
    {
        Options = options;
        Codec = new TransportCodec();
        Result = new TransportNodeResult
        {
            Role = options.Role,
            InstanceId = options.InstanceId,
            Seed = options.Seed,
            ProcessId = Environment.ProcessId,
            HighestGeneration = 1,
            RuntimeArtifactSetDigest = ProcessPeerArtifactManifestFile.CurrentArtifactSetDigest(),
            RuntimeIdentity = ProcessRuntimeIdentity.CaptureCurrent()
        };
    }

    protected void Send<TPayload>(
        NetPeer peer,
        string instanceId,
        int generation,
        long sequence,
        TransportMessageKind kind,
        TPayload payload)
    {
        TransportEncodedFrame frame = Codec.Encode(instanceId, generation, sequence, kind, payload);
        Result.WireFrames.Add(ToEvidence("sent", frame));
        peer.Send(frame.WireBytes, DeliveryMethod.ReliableOrdered);
    }

    protected void RecordReceived(TransportDecodedFrame frame)
    {
        Result.WireFrames.Add(new TransportWireFrameEvidence
        {
            Direction = "received",
            Kind = frame.Envelope.Kind.ToString(),
            InstanceId = frame.Envelope.InstanceId,
            Generation = frame.Envelope.Generation,
            Sequence = frame.Envelope.Sequence,
            WireSha256 = frame.WireSha256,
            PayloadSha256 = frame.PayloadSha256
        });
    }

    protected void RecordDeliveryDomain(byte channelNumber, DeliveryMethod deliveryMethod)
    {
        Result.DeliveryDomainObserved = true;
        if (channelNumber != 0 || deliveryMethod != DeliveryMethod.ReliableOrdered)
        {
            Result.DeliveryDomainValid = false;
        }
    }

    protected void RecordMalformed(byte[] wireBytes)
    {
        Result.WireFrames.Add(new TransportWireFrameEvidence
        {
            Direction = "received",
            Kind = "Malformed",
            InstanceId = string.Empty,
            WireSha256 = Sha256(wireBytes),
            PayloadSha256 = string.Empty
        });
    }

    protected void RecordRawSent(byte[] wireBytes, string kind)
    {
        Result.WireFrames.Add(new TransportWireFrameEvidence
        {
            Direction = "sent",
            Kind = kind,
            InstanceId = Options.InstanceId,
            Generation = 1,
            Sequence = 0,
            WireSha256 = Sha256(wireBytes),
            PayloadSha256 = string.Empty
        });
    }

    private static TransportWireFrameEvidence ToEvidence(string direction, TransportEncodedFrame frame)
    {
        return new TransportWireFrameEvidence
        {
            Direction = direction,
            Kind = frame.Envelope.Kind.ToString(),
            InstanceId = frame.Envelope.InstanceId,
            Generation = frame.Envelope.Generation,
            Sequence = frame.Envelope.Sequence,
            WireSha256 = frame.WireSha256,
            PayloadSha256 = frame.PayloadSha256
        };
    }

    private static string Sha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public abstract void OnPeerConnected(NetPeer peer);
    public abstract void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo);
    public abstract void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod);
    public abstract void OnConnectionRequest(ConnectionRequest request);

    public virtual void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
    {
        if (Result.Error == null) Result.Error = $"network-error:{socketError}";
    }

    public virtual void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
    }

    public virtual void OnNetworkReceiveUnconnected(
        IPEndPoint remoteEndPoint,
        NetPacketReader reader,
        UnconnectedMessageType messageType)
    {
    }
}
