using GameInterface.Services.Issues.Quests.ExtortionByDeserters.Messages;
using ProtoBuf;
using System;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class ExtortionMessageSerializationTests
{
    [Theory]
    [InlineData((byte)ExtortionByDesertersNotice.None, (byte)0)]
    [InlineData((byte)ExtortionByDesertersNotice.DesertersNoticedPlayer, (byte)1)]
    [InlineData((byte)ExtortionByDesertersNotice.PlayerLeftVillage, (byte)2)]
    [InlineData((byte)ExtortionByDesertersNotice.Ambush, (byte)4)]
    public void StateNoticePreservesItsWireValue(byte notice, byte expected)
    {
        var message = new NetworkExtortionByDesertersState(null!, null!, null!, null!, null!, 0, null!, notice);
        using var stream = new MemoryStream();

        Serializer.Serialize(stream, message);

        // The notice is protobuf field 8 with varint wire type.
        Assert.Equal(expected == 0 ? Array.Empty<byte>() : new byte[] { 0x40, expected }, stream.ToArray());
        stream.Position = 0;
        Assert.Equal(expected, Serializer.Deserialize<NetworkExtortionByDesertersState>(stream).Notice);
    }
}
