using VerificationHarness.Serialization;

namespace VerificationHarness.DedicatedServerSynthetic;

public sealed record DedicatedServerWireEntry(
    string Name,
    string FullTypeName,
    int TypeId,
    byte Channel,
    string DeliveryMethod,
    string Direction,
    bool Optional);

public static class DedicatedServerWireManifest
{
    public const string Version = "bannerlord-coop.ds-synthetic-wire.v1";

    public const int CampaignTimePacketTypeId = 488231864;
    public const int NetworkModuleVersionsValidateTypeId = 1457133576;
    public const int NetworkModuleVersionsValidatedTypeId = 1206877260;
    public const int NetworkClientValidateTypeId = 791628818;
    public const int NetworkClientValidatedTypeId = 29530214;
    public const int NetworkSessionLobbyChangedTypeId = 1547717120;
    public const int AggregateMessagePacketTypeId = 1253361833;

    private static readonly IReadOnlyList<DedicatedServerWireEntry> entries = Array.AsReadOnly(new[]
    {
        new DedicatedServerWireEntry(
            "campaign-time",
            "Coop.Core.Common.Network.Packets.CampaignTimePacket",
            CampaignTimePacketTypeId,
            0,
            "Sequenced",
            "server-to-client",
            false),
        new DedicatedServerWireEntry(
            "module-versions-validate",
            "Coop.Core.Server.Connections.Messages.NetworkModuleVersionsValidate",
            NetworkModuleVersionsValidateTypeId,
            0,
            "ReliableOrdered",
            "client-to-server",
            false),
        new DedicatedServerWireEntry(
            "module-versions-validated",
            "Coop.Core.Server.Connections.Messages.NetworkModuleVersionsValidated",
            NetworkModuleVersionsValidatedTypeId,
            0,
            "ReliableOrdered",
            "server-to-client",
            false),
        new DedicatedServerWireEntry(
            "client-validate",
            "Coop.Core.Server.Connections.Messages.NetworkClientValidate",
            NetworkClientValidateTypeId,
            0,
            "ReliableOrdered",
            "client-to-server",
            false),
        new DedicatedServerWireEntry(
            "client-validated",
            "Coop.Core.Server.Connections.Messages.NetworkClientValidated",
            NetworkClientValidatedTypeId,
            0,
            "ReliableOrdered",
            "server-to-client",
            false),
        new DedicatedServerWireEntry(
            "session-lobby-changed",
            "Coop.Core.Common.Session.Messages.NetworkSessionLobbyChanged",
            NetworkSessionLobbyChangedTypeId,
            0,
            "ReliableOrdered",
            "server-to-client",
            true),
        new DedicatedServerWireEntry(
            "aggregate-message",
            "Common.PacketHandlers.AggregateMessagePacket",
            AggregateMessagePacketTypeId,
            0,
            "ReliableOrdered",
            "both",
            false),
    });

    public static IReadOnlyList<DedicatedServerWireEntry> Entries => entries;

    public static string Sha256 { get; } = new CanonicalJsonHasher().ComputeSha256(new
    {
        version = Version,
        entries
    });

    public static DedicatedServerWireEntry GetByTypeId(int typeId)
    {
        DedicatedServerWireEntry? entry = entries.SingleOrDefault(x => x.TypeId == typeId);
        return entry ?? throw new InvalidDataException($"Type id {typeId} is not in {Version}.");
    }

    public static int ComputeTypeId(string fullTypeName)
    {
        if (string.IsNullOrWhiteSpace(fullTypeName))
        {
            throw new ArgumentException("A full type name is required.", nameof(fullTypeName));
        }

        const uint prime = 16777619;
        uint hash = 2166136261;
        foreach (char character in fullTypeName)
        {
            hash = (hash ^ (byte)character) * prime;
            hash = (hash ^ (byte)(character >> 8)) * prime;
        }

        return (int)(hash & 0x7FFFFFFF);
    }
}
