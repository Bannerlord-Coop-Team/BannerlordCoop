using Autofac;
using Common.Util;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using Serilog;
using System;
using System.IO;
using System.Text;
using TaleWorlds.CampaignSystem;
using Xunit;
using CampaignKingdomDecision = TaleWorlds.CampaignSystem.Election.KingdomDecision;

namespace GameInterface.Tests.Services.Kingdoms.KingdomDecision
{
    /// <summary>
    /// Every test that touches the process wide <see cref="KingdomDecisionTypeRegistry"/> lives in this class, so
    /// they never run in parallel with each other.
    /// </summary>
    public class RegisteredKingdomDecisionSerializationTest : IDisposable
    {
        public void Dispose()
        {
            KingdomDecisionTypeRegistry.Unregister<CouncilSeatDecision>();
            KingdomDecisionTypeRegistry.Unregister<OtherModDecision>();
        }

        [Fact]
        public void RegisteredModDecision_RoundTripsThroughTheNetworkData()
        {
            ObjectManager objectManager = CreateObjectManager(out Clan proposerClan, out Kingdom kingdom);
            CouncilSeatDecision decision = ObjectHelper.SkipConstructor<CouncilSeatDecision>();
            decision.ProposerClan = proposerClan;
            decision._kingdom = kingdom;
            decision.TriggerTime = new CampaignTime(42);
            decision.IsEnforced = true;
            decision.Note = "council seat";

            var converter = new KingdomDecisionDataConverter(objectManager);
            Assert.False(converter.IsSupported(decision));
            RegisterCouncilSeat();
            Assert.True(converter.IsSupported(decision));

            var memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, converter.Convert(decision));
            memoryStream.Position = 0;
            KingdomDecisionData received = Serializer.Deserialize<KingdomDecisionData>(memoryStream);
            Assert.IsType<RegisteredKingdomDecisionData>(received);

            Assert.True(new KingdomDecisionDataConverter(objectManager).TryGetKingdomDecision(received, out CampaignKingdomDecision rebuilt));
            var rebuiltDecision = Assert.IsType<CouncilSeatDecision>(rebuilt);
            Assert.Equal("council seat", rebuiltDecision.Note);
            Assert.Same(proposerClan, rebuiltDecision.ProposerClan);
            Assert.Same(kingdom, rebuiltDecision.Kingdom);
            Assert.Equal(42, rebuiltDecision.TriggerTime._numTicks);
            Assert.True(rebuiltDecision.IsEnforced);
        }

        [Fact]
        public void RegisteredModDecision_WithoutTheRegistration_IsNotRebuilt()
        {
            ObjectManager objectManager = CreateObjectManager(out Clan proposerClan, out Kingdom kingdom);
            var data = new RegisteredKingdomDecisionData(proposerClan.StringId, kingdom.StringId, 42, false, false, false,
                typeof(CouncilSeatDecision).FullName, Encoding.UTF8.GetBytes("council seat"));

            Assert.False(new KingdomDecisionDataConverter(objectManager).TryGetKingdomDecision(data, out CampaignKingdomDecision rebuilt));
            Assert.Null(rebuilt);
        }

        [Fact]
        public void Registration_SurvivesANewCoopContainer()
        {
            RegisterCouncilSeat();
            CouncilSeatDecision decision = ObjectHelper.SkipConstructor<CouncilSeatDecision>();

            // Coop builds a new container on every connect, as after a reconnect.
            for (int session = 0; session < 2; session++)
            {
                var builder = new ContainerBuilder();
                builder.RegisterInstance(new ObjectManager(new LoggerConfiguration().CreateLogger())).As<IObjectManager>();
                builder.RegisterType<KingdomDecisionDataConverter>().As<IKingdomDecisionDataConverter>().InstancePerLifetimeScope();
                using var container = builder.Build();
                using var scope = container.BeginLifetimeScope();

                Assert.True(scope.Resolve<IKingdomDecisionDataConverter>().IsSupported(decision));
            }
        }

        [Fact]
        public void Matches_SameRegisteredTypes_AllowsTheJoin()
        {
            RegisterCouncilSeat();

            Assert.True(KingdomDecisionTypeRegistry.Matches(new[] { typeof(CouncilSeatDecision).FullName }, out string reason));
            Assert.Null(reason);
        }

        [Fact]
        public void Matches_ServerRegisteredClientNot_RefusesTheJoin()
        {
            RegisterCouncilSeat();

            Assert.False(KingdomDecisionTypeRegistry.Matches(null, out string reason));
            Assert.Contains($"The server syncs kingdom decision type '{typeof(CouncilSeatDecision).FullName}'", reason);
        }

        [Fact]
        public void Matches_ClientRegisteredServerNot_RefusesTheJoin()
        {
            Assert.False(KingdomDecisionTypeRegistry.Matches(new[] { typeof(OtherModDecision).FullName }, out string reason));
            Assert.Contains($"This client syncs kingdom decision type '{typeof(OtherModDecision).FullName}'", reason);
        }

        private static void RegisterCouncilSeat()
        {
            KingdomDecisionTypeRegistry.Register<CouncilSeatDecision>(
                decision => Encoding.UTF8.GetBytes(decision.Note ?? string.Empty),
                payload =>
                {
                    CouncilSeatDecision decision = ObjectHelper.SkipConstructor<CouncilSeatDecision>();
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

        public class CouncilSeatDecision : TestModKingdomDecision
        {
            public CouncilSeatDecision(Clan proposerClan) : base(proposerClan) { }
        }

        public class OtherModDecision : TestModKingdomDecision
        {
            public OtherModDecision(Clan proposerClan) : base(proposerClan) { }
        }
    }
}
