using Common.Util;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using Serilog;
using System.IO;
using System.Text;
using TaleWorlds.CampaignSystem;
using Xunit;
using CampaignKingdomDecision = TaleWorlds.CampaignSystem.Election.KingdomDecision;

namespace GameInterface.Tests.Services.Kingdoms.KingdomDecision
{
    public class RegisteredKingdomDecisionSerializationTest
    {
        [Fact]
        public void RegisteredModDecision_RoundTripsThroughTheNetworkData()
        {
            ObjectManager objectManager = CreateObjectManager(out Clan proposerClan, out Kingdom kingdom);
            TestModKingdomDecision decision = ObjectHelper.SkipConstructor<TestModKingdomDecision>();
            decision.ProposerClan = proposerClan;
            decision._kingdom = kingdom;
            decision.TriggerTime = new CampaignTime(42);
            decision.IsEnforced = true;
            decision.Note = "council seat";

            var serverConverter = new KingdomDecisionDataConverter(objectManager);
            Assert.False(serverConverter.IsSupported(decision));
            RegisterTestDecision(serverConverter);
            Assert.True(serverConverter.IsSupported(decision));

            var memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, serverConverter.Convert(decision));
            memoryStream.Position = 0;
            KingdomDecisionData received = Serializer.Deserialize<KingdomDecisionData>(memoryStream);
            Assert.IsType<RegisteredKingdomDecisionData>(received);

            var clientConverter = new KingdomDecisionDataConverter(objectManager);
            RegisterTestDecision(clientConverter);
            Assert.True(clientConverter.TryGetKingdomDecision(received, out CampaignKingdomDecision rebuilt));

            var rebuiltDecision = Assert.IsType<TestModKingdomDecision>(rebuilt);
            Assert.Equal("council seat", rebuiltDecision.Note);
            Assert.Same(proposerClan, rebuiltDecision.ProposerClan);
            Assert.Same(kingdom, rebuiltDecision.Kingdom);
            Assert.Equal(42, rebuiltDecision.TriggerTime._numTicks);
            Assert.True(rebuiltDecision.IsEnforced);
        }

        [Fact]
        public void RegisteredModDecision_ClientWithoutTheRegistration_DoesNotRebuildIt()
        {
            ObjectManager objectManager = CreateObjectManager(out Clan proposerClan, out Kingdom kingdom);
            var data = new RegisteredKingdomDecisionData(proposerClan.StringId, kingdom.StringId, 42, false, false, false,
                typeof(TestModKingdomDecision).FullName, Encoding.UTF8.GetBytes("council seat"));

            Assert.False(new KingdomDecisionDataConverter(objectManager).TryGetKingdomDecision(data, out CampaignKingdomDecision rebuilt));
            Assert.Null(rebuilt);
        }

        private static void RegisterTestDecision(IKingdomDecisionDataConverter converter)
        {
            converter.Register<TestModKingdomDecision>(
                decision => Encoding.UTF8.GetBytes(decision.Note),
                payload =>
                {
                    TestModKingdomDecision decision = ObjectHelper.SkipConstructor<TestModKingdomDecision>();
                    decision.Note = Encoding.UTF8.GetString(payload);
                    return decision;
                });
        }

        private static ObjectManager CreateObjectManager(out Clan proposerClan, out Kingdom kingdom)
        {
            ObjectManager objectManager = new ObjectManager(new LoggerConfiguration().CreateLogger());
            proposerClan = ObjectHelper.SkipConstructor<Clan>();
            proposerClan.StringId = "ProposerClan";
            kingdom = ObjectHelper.SkipConstructor<Kingdom>();
            kingdom.StringId = "Kingdom";
            objectManager.AddExisting(proposerClan.StringId, proposerClan);
            objectManager.AddExisting(kingdom.StringId, kingdom);
            return objectManager;
        }
    }
}
