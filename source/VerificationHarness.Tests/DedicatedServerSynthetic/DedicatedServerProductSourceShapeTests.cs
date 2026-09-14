using System.Text.RegularExpressions;
using VerificationHarness.DedicatedServerSynthetic;

namespace VerificationHarness.Tests.DedicatedServerSynthetic;

public sealed class DedicatedServerProductSourceShapeTests
{
    [Fact]
    public void ProductSourceShapeMatchesTheFrozenSyntheticContract()
    {
        AssertSource(
            "source/Coop.Core/Common/Network/Packets/CampaignTimePacket.cs",
            @"namespace\s+Coop\.Core\.Common\.Network\.Packets",
            @"struct\s+CampaignTimePacket\s*:\s*IPacket",
            @"DeliveryMethod\s*=>\s*DeliveryMethod\.Sequenced",
            @"\[ProtoMember\(1\)\]\s*public\s+readonly\s+long\s+ServerTicks",
            @"\[ProtoMember\(2\)\]\s*public\s+readonly\s+int\s+JoinPacketsRemaining");
        AssertSource(
            "source/Coop.Core/Server/Connections/Messages/NetworkModuleVersionsValidate.cs",
            @"namespace\s+Coop\.Core\.Server\.Connections\.Messages",
            @"record\s+NetworkModuleVersionsValidate\s*:\s*ICommand",
            @"\[ProtoMember\(1\)\]\s*public\s+NetworkModuleInfo\[\]\s+Modules",
            @"\[ProtoMember\(2\)\]\s*public\s+string\?\s+CoopBuildVersion");
        AssertSource(
            "source/Coop.Core/Server/Connections/Messages/NetworkModuleVersionsValidated.cs",
            @"record\s+NetworkModuleVersionsValidated\s*:\s*IEvent",
            @"\[ProtoMember\(1\)\]\s*public\s+bool\s+Matches",
            @"\[ProtoMember\(2\)\]\s*public\s+string\?\s+Reason",
            @"\[ProtoMember\(3\)\]\s*public\s+string\?\s+CoopBuildVersion");
        AssertSource(
            "source/Coop.Core/Server/Connections/Messages/NetworkClientValidation.cs",
            @"record\s+NetworkClientValidate\s*:\s*ICommand",
            @"\[ProtoMember\(1\)\]\s*public\s+string\s+PlayerId",
            @"record\s+NetworkClientValidated\s*:\s*IEvent",
            @"\[ProtoMember\(1\)\]\s*public\s+bool\s+HeroExists",
            @"\[ProtoMember\(2\)\]\s*public\s+Player\s+Player");
        AssertSource(
            "source/Coop.Core/Common/Session/Messages/NetworkSessionLobbyChanged.cs",
            @"namespace\s+Coop\.Core\.Common\.Session\.Messages",
            @"struct\s+NetworkSessionLobbyChanged\s*:\s*IEvent",
            @"\[ProtoMember\(1\)\]\s*public\s+readonly\s+ulong\s+LobbyId");
        AssertSource(
            "source/Common/PacketHandlers/AggregateMessagePacketHandler.cs",
            @"namespace\s+Common\.PacketHandlers",
            @"struct\s+AggregateMessagePacket\s*:\s*IPacket",
            @"DeliveryMethod\s*=>\s*DeliveryMethod\.ReliableOrdered",
            @"\[ProtoMember\(1\)\]\s*public\s+readonly\s+byte\[\]\[\]\s+Messages");
        AssertSource(
            "source/Common/PacketHandlers/MessagePacketHandler.cs",
            @"struct\s+MessagePacket\s*:\s*IPacket",
            @"DeliveryMethod\s*=>\s*DeliveryMethod\.ReliableOrdered");
        AssertSource(
            "source/Common/Serialization/ProtoBufSerializer.cs",
            @"\[ProtoMember\(1\)\]\s*public\s+int\s+TypeId",
            @"\[ProtoMember\(2\)\]\s*public\s+byte\[\]\s+Data");
        AssertSource(
            "source/Common/Serialization/SerializableTypeMapper.cs",
            @"StableId\s*\(\s*string\s+fullName\s*\)",
            @"const\s+uint\s+prime\s*=\s*16777619",
            @"uint\s+hash\s*=\s*2166136261",
            @"hash\s*=\s*\(hash\s*\^\s*\(byte\)c\)\s*\*\s*prime",
            @"hash\s*=\s*\(hash\s*\^\s*\(byte\)\(c\s*>>\s*8\)\)\s*\*\s*prime",
            @"return\s+\(int\)\(hash\s*&\s*0x7FFFFFFF\)");

        foreach (DedicatedServerWireEntry entry in DedicatedServerWireManifest.Entries)
        {
            Assert.Equal(entry.TypeId, DedicatedServerWireManifest.ComputeTypeId(entry.FullTypeName));
        }
    }

    [Fact]
    public void FrozenSyntheticSamplesHaveStableWireBytes()
    {
        var codec = new DedicatedServerWireCodec();
        byte[] heartbeat = codec.EncodeCampaignTime(123456789, -1);
        byte[] moduleRequest = codec.EncodeModuleMismatchRequest("intentional-mismatch");
        byte[] moduleResult = codec.EncodeModuleValidationResult(false, "denied", "server-build");
        byte[] clientRequest = codec.EncodeClientValidationRequest("ds-synthetic-client-a");
        byte[] clientResult = codec.EncodeFreshClientValidationResult();
        string actual = string.Join("\n", new[]
        {
            Convert.ToHexString(heartbeat).ToLowerInvariant(),
            Convert.ToHexString(moduleRequest).ToLowerInvariant(),
            Convert.ToHexString(moduleResult).ToLowerInvariant(),
            Convert.ToHexString(clientRequest).ToLowerInvariant(),
            Convert.ToHexString(clientResult).ToLowerInvariant()
        });

        const string expected =
            "08b8a7e7e801121008959aef3a10ffffffffffffffffff01\n" +
            "0888b0e8b60512161214696e74656e74696f6e616c2d6d69736d61746368\n" +
            "08ccf8bdbf041216120664656e6965641a0c7365727665722d6275696c64\n" +
            "089298bdf90212170a1564732d73796e7468657469632d636c69656e742d61\n" +
            "08e6b08a0e1200";
        Assert.Equal(expected, actual);
    }

    private static void AssertSource(string relativePath, params string[] patterns)
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            ".."));
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        foreach (string pattern in patterns)
        {
            Assert.Matches(new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Singleline), source);
        }
    }
}
