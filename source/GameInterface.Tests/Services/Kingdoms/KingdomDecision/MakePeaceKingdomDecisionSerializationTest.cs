using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;
using System.IO;
using System.Reflection;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms.KingdomDecision
{
    public class MakePeaceKingdomDecisionSerializationTest
    {
        [Fact]
        public void SerializeMakePeaceKingdomDecisionWithClanFaction()
        {
            MakePeaceKingdomDecisionData makePeaceKingdomDecisionData = new MakePeaceKingdomDecisionData("ProposerClan", "Kingdom", 10, true, true, true, "Clan1", 100, true);
            KingdomDecisionData kingdomDecisionDerivedData = makePeaceKingdomDecisionData;
            MemoryStream memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, kingdomDecisionDerivedData);
            memoryStream.Position = 0;
            KingdomDecisionData obj = Serializer.Deserialize<KingdomDecisionData>(memoryStream);
            Assert.True(obj is MakePeaceKingdomDecisionData);
            MakePeaceKingdomDecisionData deserializedObj = (MakePeaceKingdomDecisionData)obj;
            Assert.Equal(makePeaceKingdomDecisionData.ProposerClanId, deserializedObj.ProposerClanId);
            Assert.Equal(makePeaceKingdomDecisionData.KingdomId, deserializedObj.KingdomId);
            Assert.Equal(makePeaceKingdomDecisionData.PlayerExamined, deserializedObj.PlayerExamined);
            Assert.Equal(makePeaceKingdomDecisionData.TriggerTime, deserializedObj.TriggerTime);
            Assert.Equal(makePeaceKingdomDecisionData.NotifyPlayer, deserializedObj.NotifyPlayer);
            Assert.Equal(makePeaceKingdomDecisionData.IsEnforced, deserializedObj.IsEnforced);
            Assert.Equal(makePeaceKingdomDecisionData.DailyTributeToBePaid, deserializedObj.DailyTributeToBePaid);
            Assert.Equal(makePeaceKingdomDecisionData.ApplyResults, deserializedObj.ApplyResults);
            Assert.Equal(makePeaceKingdomDecisionData.FactionToMakePeaceWithId, deserializedObj.FactionToMakePeaceWithId);
        }

        [Fact]
        public void MakePeaceKingdomDecisionDataReflectionTests()
        {
            FieldInfo? fieldInfo = typeof(MakePeaceKingdomDecisionData).GetField("FactionToMakePeaceWithField", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo);
            FieldInfo? fieldInfo2 = typeof(MakePeaceKingdomDecisionData).GetField("DailyTributeToBePaidField", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo2);
            FieldInfo? fieldInfo3 = typeof(MakePeaceKingdomDecisionData).GetField("ApplyResultsField", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo3);
            object? obj = fieldInfo?.GetValue(null);
            Assert.NotNull(obj);
            object? obj2 = fieldInfo2?.GetValue(null);
            Assert.NotNull(obj2);
            object? obj3 = fieldInfo3?.GetValue(null);
            Assert.NotNull(obj3);
        }
    }
}
