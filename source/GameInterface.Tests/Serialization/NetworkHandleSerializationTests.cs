using GameInterface.Services.TroopRosters.Messages;
using ProtoBuf;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class NetworkHandleSerializationTests
{
    [Fact]
    public void TroopRosterBatch_HandlesUseLessWireSpaceThanStringIds()
    {
        var operation = TroopRosterElementOperation.AddCounts(3, 1, 40, false);
        var legacy = new LegacyTroopRosterElementBatch(
            "TroopRoster_MobileParty_looters1_1_members",
            "CharacterObject_imperial_infantryman",
            new[] { operation });
        var handles = new NetworkTroopRosterElementBatch(321, 654, new[] { operation });

        int legacyBytes = SerializedSize(legacy);
        int handleBytes = SerializedSize(handles);

        Assert.True(
            handleBytes < legacyBytes,
            $"Expected handle payload ({handleBytes} bytes) below string-id payload ({legacyBytes} bytes).");
    }

    private static int SerializedSize<T>(T value)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, value);
        return checked((int)stream.Length);
    }

    [ProtoContract(SkipConstructor = true)]
    private readonly struct LegacyTroopRosterElementBatch
    {
        [ProtoMember(1)]
        public readonly string TroopRosterId;

        [ProtoMember(2)]
        public readonly string CharacterId;

        [ProtoMember(3)]
        public readonly TroopRosterElementOperation[] Operations;

        public LegacyTroopRosterElementBatch(
            string troopRosterId,
            string characterId,
            TroopRosterElementOperation[] operations)
        {
            TroopRosterId = troopRosterId;
            CharacterId = characterId;
            Operations = operations;
        }
    }
}
