using GameInterface.Services.Kingdoms.Data;
using System.Reflection;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms.KingdomDecision
{
    public class KingdomDecisionBaseTest
    {
        [Fact]
        public void KingdomDecisionDataReflectionTests()
        {
            FieldInfo? fieldInfo = typeof(KingdomDecisionData).GetField("SetKingdomMethod", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo);
            FieldInfo? fieldInfo2 = typeof(KingdomDecisionData).GetField("SetProposerClanMethod", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo2);
            FieldInfo? fieldInfo3 = typeof(KingdomDecisionData).GetField("SetTriggerTimeMethod", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo3);
            FieldInfo? fieldInfo4 = typeof(KingdomDecisionData).GetField("CampaignTimeCtr", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo4);
            object? obj = fieldInfo?.GetValue(null);
            Assert.NotNull(obj);
            object? obj2 = fieldInfo2?.GetValue(null);
            Assert.NotNull(obj2);
            object? obj3 = fieldInfo3?.GetValue(null);
            Assert.NotNull(obj3);
            object? obj4 = fieldInfo4?.GetValue(null);
            Assert.NotNull(obj4);
        }
    }
}
