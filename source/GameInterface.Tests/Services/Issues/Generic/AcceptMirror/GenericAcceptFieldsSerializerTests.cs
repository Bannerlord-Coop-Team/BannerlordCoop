using GameInterface.Services.Issues.Generic.AcceptMirror;
using ProtoBuf;
using Xunit;

namespace GameInterface.Tests.Services.Issues.Generic.AcceptMirror;

public class GenericAcceptFieldsSerializerTests
{
    [ProtoContract(SkipConstructor = true)]
    private readonly struct Fields
    {
        [ProtoMember(1)]
        public readonly int RequestedItemAmount;
        [ProtoMember(2)]
        public readonly int RewardGold;

        public Fields(int requestedItemAmount, int rewardGold)
        {
            RequestedItemAmount = requestedItemAmount;
            RewardGold = rewardGold;
        }
    }

    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsTheValue()
    {
        var fields = new Fields(7, 350);

        var bytes = GenericAcceptFieldsSerializer.Serialize(fields);
        var result = GenericAcceptFieldsSerializer.Deserialize<Fields>(bytes);

        Assert.Equal(fields.RequestedItemAmount, result.RequestedItemAmount);
        Assert.Equal(fields.RewardGold, result.RewardGold);
    }

    [Fact]
    public void Deserialize_NullBytes_ReturnsDefaultInsteadOfThrowing()
    {
        var result = GenericAcceptFieldsSerializer.Deserialize<Fields>(null);

        Assert.Equal(default, result.RequestedItemAmount);
        Assert.Equal(default, result.RewardGold);
    }

    [Fact]
    public void Deserialize_MalformedBytes_Throws()
    {
        var garbage = new byte[] { 0xFF, 0x00, 0xAB, 0xCD, 0x12 };

        Assert.ThrowsAny<System.Exception>(() => GenericAcceptFieldsSerializer.Deserialize<Fields>(garbage));
    }
}
