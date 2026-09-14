using ProtoBuf;
using VerificationHarness.DedicatedServerSynthetic;

namespace VerificationHarness.Tests.DedicatedServerSynthetic;

public sealed class DedicatedServerWireCodecTests
{
    private readonly DedicatedServerWireCodec codec = new();

    [Fact]
    public void SafeSubset_RoundTripsThroughCommonEnvelopeShape()
    {
        byte[] heartbeat = codec.EncodeCampaignTime(123456789, -1);
        byte[] moduleRequest = codec.EncodeModuleMismatchRequest("intentional-mismatch");
        byte[] moduleResult = codec.EncodeModuleValidationResult(false, "denied", "server-build");
        byte[] clientRequest = codec.EncodeClientValidationRequest("ds-synthetic-client-a");
        byte[] clientResult = codec.EncodeFreshClientValidationResult();
        byte[] lobbyChanged = codec.EncodeSessionLobbyChanged(1234);

        Assert.Equal(
            new DedicatedCampaignTime(123456789, -1),
            codec.DecodeCampaignTime(heartbeat));
        Assert.Equal(
            new DedicatedModuleValidationRequest(0, "intentional-mismatch"),
            codec.DecodeModuleValidationRequest(moduleRequest));
        Assert.Equal(
            new DedicatedModuleValidationResult(false, "denied", "server-build"),
            codec.DecodeModuleValidationResult(moduleResult));
        Assert.Equal("ds-synthetic-client-a", codec.DecodeClientValidationRequest(clientRequest));
        Assert.Equal(
            new DedicatedClientValidationResult(false, false),
            codec.DecodeClientValidationResult(clientResult));
        Assert.Equal((ulong)1234, codec.DecodeSessionLobbyChanged(lobbyChanged));

        IReadOnlyList<byte[]> aggregate = codec.DecodeAggregate(
            codec.EncodeAggregate(new[] { moduleRequest, clientRequest, lobbyChanged }));
        Assert.Equal(3, aggregate.Count);
        Assert.Equal(moduleRequest, aggregate[0]);
        Assert.Equal(clientRequest, aggregate[1]);
        Assert.Equal(lobbyChanged, aggregate[2]);
    }

    [Fact]
    public void CompatibleModuleRequest_PreservesEveryProductionField()
    {
        DedicatedModuleValidationContract contract = ModuleContract();

        byte[] wireBytes = codec.EncodeModuleValidationRequest(contract);
        DedicatedModuleValidationContract decoded = codec.DecodeModuleValidationContract(wireBytes);

        Assert.True(DedicatedModuleValidationContracts.Equivalent(contract, decoded));
        Assert.Equal(2, codec.DecodeModuleValidationRequest(wireBytes).ModuleCount);
    }

    [Fact]
    public void SuccessfulModuleResponse_AllowsTheProductionNullReason()
    {
        byte[] wireBytes = codec.EncodeModuleValidationResult(true, string.Empty, "coop-build");

        DedicatedModuleValidationResult decoded = codec.DecodeModuleValidationResult(wireBytes);

        Assert.True(decoded.Matches);
        Assert.Equal(string.Empty, decoded.Reason);
        Assert.Equal("coop-build", decoded.ServerBuildVersion);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ExistingPlayerValidation_IsRejectedFromEncodedWire(
        bool heroExists,
        bool playerPayloadPresent)
    {
        byte[] wireBytes = EncodeTestEnvelope(
            DedicatedServerWireManifest.NetworkClientValidatedTypeId,
            new DedicatedClientValidationResultPayload
            {
                HeroExists = heroExists,
                PlayerPayload = playerPayloadPresent ? new byte[] { 1 } : null
            });
        DedicatedClientValidationResult result = codec.DecodeClientValidationResult(wireBytes);

        Assert.Throws<InvalidDataException>(() =>
            DedicatedServerSyntheticClientNode.RequireFreshControllerShortcut(result));
    }

    [Fact]
    public void ModuleContracts_PreserveAndCompareProviderOrder()
    {
        DedicatedModuleValidationContract contract = ModuleContract();
        DedicatedModuleValidationContract reordered = new(
            contract.CoopBuildVersion,
            contract.Modules.Reverse().ToArray());

        Assert.False(DedicatedModuleValidationContracts.Equivalent(contract, reordered));
        DedicatedModuleValidationContract decoded = codec.DecodeModuleValidationContract(
            codec.EncodeModuleValidationRequest(contract));
        Assert.Equal(contract.Modules, decoded.Modules);
    }

    [Fact]
    public void FailedModuleResponse_RequiresAReason()
    {
        Assert.Throws<InvalidDataException>(() =>
            codec.EncodeModuleValidationResult(false, string.Empty, "coop-build"));
    }

    [Fact]
    public void SessionLobbyChanged_RejectsZeroLobbyId()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => codec.EncodeSessionLobbyChanged(0));
    }

    [Fact]
    public void DecodeFrame_RejectsUnknownTypeId()
    {
        byte[] wireBytes;
        using (var stream = new MemoryStream())
        {
            Serializer.Serialize(stream, new TestEnvelope { TypeId = 42, Data = new byte[] { 1 } });
            wireBytes = stream.ToArray();
        }

        Assert.Throws<InvalidDataException>(() => codec.DecodeFrame(wireBytes));
    }

    [Fact]
    public void Aggregate_RejectsOversizedPayload()
    {
        var oversized = new byte[DedicatedServerWireCodec.MaximumAggregatePayloadBytes + 1];
        Assert.Throws<InvalidDataException>(() => codec.EncodeAggregate(new[] { oversized }));
    }

    [Fact]
    public void Aggregate_StopsEnumeratingAtTheMessageLimit()
    {
        byte[] message = codec.EncodeClientValidationRequest("ds-synthetic-client-a");

        Assert.Throws<ArgumentOutOfRangeException>(() => codec.EncodeAggregate(TooManyMessages(message)));
    }

    [Fact]
    public void Aggregate_RejectsNestedAggregateOnEncodeAndDecode()
    {
        byte[] message = codec.EncodeClientValidationRequest("ds-synthetic-client-a");
        byte[] nested = codec.EncodeAggregate(new[] { message });

        Assert.Throws<ArgumentException>(() => codec.EncodeAggregate(new[] { nested }));

        byte[] maliciousOuter = EncodeTestEnvelope(
            DedicatedServerWireManifest.AggregateMessagePacketTypeId,
            new TestAggregate { Messages = new[] { nested } });
        Assert.Throws<InvalidDataException>(() => codec.DecodeAggregate(maliciousOuter));
    }

    [Fact]
    public void Aggregate_RejectsPacketTypesThatProductionBatchingNeverContains()
    {
        byte[] heartbeat = codec.EncodeCampaignTime(1, -1);

        Assert.Throws<ArgumentException>(() => codec.EncodeAggregate(new[] { heartbeat }));
        byte[] maliciousOuter = EncodeTestEnvelope(
            DedicatedServerWireManifest.AggregateMessagePacketTypeId,
            new TestAggregate { Messages = new[] { heartbeat } });
        Assert.Throws<InvalidDataException>(() => codec.DecodeAggregate(maliciousOuter));
    }

    [Fact]
    public void DecodeFrame_RejectsOversizedWireInputBeforeProtobufParsing()
    {
        var oversized = new byte[DedicatedServerWireCodec.MaximumWireBytes + 1];
        Assert.Throws<InvalidDataException>(() => codec.DecodeFrame(oversized));
    }

    private static IEnumerable<byte[]> TooManyMessages(byte[] message)
    {
        for (int index = 0; index <= DedicatedServerWireCodec.MaximumAggregateMessages; index++)
        {
            yield return message;
        }

        throw new InvalidOperationException("The codec enumerated beyond its advertised message bound.");
    }

    internal static DedicatedModuleValidationContract ModuleContract()
    {
        return new DedicatedModuleValidationContract(
            "coop-build",
            new[]
            {
                new DedicatedModuleInfo(
                    "Native",
                    true,
                    false,
                    new DedicatedModuleVersion(4, 1, 2, 3, 456)),
                new DedicatedModuleInfo(
                    "Coop",
                    false,
                    false,
                    new DedicatedModuleVersion(4, 1, 2, 3, 789))
            });
    }

    private static byte[] EncodeTestEnvelope<T>(int typeId, T payload)
    {
        byte[] payloadBytes;
        using (var payloadStream = new MemoryStream())
        {
            Serializer.Serialize(payloadStream, payload);
            payloadBytes = payloadStream.ToArray();
        }

        using var wireStream = new MemoryStream();
        Serializer.Serialize(wireStream, new TestEnvelope { TypeId = typeId, Data = payloadBytes });
        return wireStream.ToArray();
    }

    [ProtoContract]
    private sealed class TestEnvelope
    {
        [ProtoMember(1)]
        public int TypeId { get; set; }

        [ProtoMember(2)]
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    [ProtoContract]
    private sealed class TestAggregate
    {
        [ProtoMember(1)]
        public byte[][] Messages { get; set; } = Array.Empty<byte[]>();
    }
}
