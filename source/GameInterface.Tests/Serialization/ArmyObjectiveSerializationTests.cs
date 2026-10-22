using GameInterface.Services.Armies.Messages;
using ProtoBuf;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class ArmyObjectiveSerializationTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("town_ES1", true)]
    [InlineData("lord_1_1_party_1", false)]
    public void ObjectiveMessage_PreservesNullAndSupportedReferences(string objectiveId, bool isSettlement)
    {
        var copy = Serializer.DeepClone(new NetworkSetArmyAiBehaviorObject("Army_Created_1", objectiveId, isSettlement));

        Assert.Equal("Army_Created_1", copy.ArmyId);
        Assert.Equal(objectiveId, copy.AiBehaviorObjectId);
        Assert.Equal(isSettlement, copy.IsSettlement);
    }
}
