using VerificationHarness.Transport;

namespace VerificationHarness.Tests.Transport;

public sealed class TransportCodecTests
{
    [Fact]
    public void CommonSerializerRoundTripsTypedEnvelopeAndTypedPayload()
    {
        var codec = new TransportCodec();
        var payload = new TransportStatePayload
        {
            StateVersion = 7,
            Marker = "state-marker"
        };

        TransportEncodedFrame encoded = codec.Encode(
            "server",
            2,
            3,
            TransportMessageKind.State,
            payload);
        TransportDecodedFrame decoded = codec.Decode(encoded.WireBytes);

        Assert.Equal(TransportCodec.CurrentProtocolVersion, decoded.Envelope.ProtocolVersion);
        Assert.Equal("server", decoded.Envelope.InstanceId);
        Assert.Equal(2, decoded.Envelope.Generation);
        Assert.Equal(3, decoded.Envelope.Sequence);
        Assert.Equal(TransportMessageKind.State, decoded.Envelope.Kind);
        var decodedPayload = Assert.IsType<TransportStatePayload>(decoded.Payload);
        Assert.Equal(7, decodedPayload.StateVersion);
        Assert.Equal("state-marker", decodedPayload.Marker);
        Assert.Equal(encoded.WireSha256, decoded.WireSha256);
        Assert.Equal(encoded.PayloadSha256, decoded.PayloadSha256);
        Assert.Equal(64, encoded.WireSha256.Length);
        Assert.Equal(64, encoded.PayloadSha256.Length);
    }

    [Fact]
    public void StateDigestIncludesOnlyDeterministicStateFields()
    {
        var codec = new TransportCodec();
        var first = new TransportStatePayload { StateVersion = 1, Marker = "same" };
        var equivalent = new TransportStatePayload { StateVersion = 1, Marker = "same" };
        var diverged = new TransportStatePayload { StateVersion = 1, Marker = "different" };

        string firstDigest = codec.ComputeStateDigest(first);
        string equivalentDigest = codec.ComputeStateDigest(equivalent);
        string divergedDigest = codec.ComputeStateDigest(diverged);

        Assert.Equal(firstDigest, equivalentDigest);
        Assert.NotEqual(firstDigest, divergedDigest);
        Assert.Equal(64, firstDigest.Length);
    }

    [Fact]
    public void MalformedFrameFailsClosed()
    {
        var codec = new TransportCodec();

        Assert.ThrowsAny<Exception>(() => codec.Decode(new byte[] { 0xFF, 0x00, 0xFE, 0x01 }));
    }
}
