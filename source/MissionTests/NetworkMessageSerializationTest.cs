using Autofac;
using Common.Messaging;
using Common.Serialization;
using GameInterface.Missions.Agents.Messages;
using GameInterface.Missions.Services.Network.Data;
using GameInterface.Missions.Services.Network.Messages;
using GameInterface.Serialization;
using GameInterface.Services.ObjectManager;
using GameInterface.Surrogates;

namespace IntroductionServerTests
{
    public class NetworkMessageSerializationTest
    {
        private ProtoBufSerializer serializer;

        public NetworkMessageSerializationTest()
        {
            serializer = new ProtoBufSerializer(new SerializableTypeMapper());

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterType<MessageBroker>().As<IMessageBroker>().SingleInstance();
            // ObjectManager takes a Serilog ILogger; production injects it via GameInterfaceModule's
            // parameter resolver, so this standalone container has to register one itself.
            builder.RegisterInstance(Common.Logging.LogManager.GetLogger<GameInterface.Services.ObjectManager.ObjectManager>()).As<Serilog.ILogger>();
            builder.RegisterType<GameInterface.Services.ObjectManager.ObjectManager>().As<IObjectManager>().InstancePerLifetimeScope();
            builder.RegisterType<BinaryPackageFactory>().As<IBinaryPackageFactory>().AutoActivate().SingleInstance();

            // The binary-package surrogates (Blow/AttackCollisionData) resolve the factory through this container.
            GameInterface.ContainerProvider.SetContainer(builder.Build());

            // Register every ProtoBuf surrogate centrally — same collection GameInterfaceModule AutoActivates
            // in the live container. Idempotent (guarded by CanSerialize), so safe across test instances.
            new SurrogateCollection();
        }

        [Fact]
        public void Serialize_Test()
        {
            // NetworkMissionJoinInfo now carries the character's object-manager id (a plain string),
            // not the CharacterObject itself — the receiver resolves it via IObjectManager. So this is a
            // straight string round-trip; no CharacterObject surrogate/registration needed.
            const string characterObjectId = "Test Character";
            var agentId = Guid.NewGuid();

            NetworkMissionJoinInfo missionJoinInfo = new NetworkMissionJoinInfo(
                characterObjectId,
                default,
                default,
                default,
                default,
                Array.Empty<CoopAgentSpawnData>());

            byte[] bytes = serializer.Serialize(missionJoinInfo);

            Assert.NotNull(bytes);

            NetworkMissionJoinInfo newEvent = (NetworkMissionJoinInfo)serializer.Deserialize(bytes);

            Assert.Equal(characterObjectId, newEvent.CharacterObjectId);
            Assert.Equal(agentId, newEvent.AgentId);
        }

        [Fact]
        public void Serialize2_Test()
        {
            var attackerGuid = Guid.NewGuid().ToString();

            NetworkDamageAgent missionJoinInfo = new NetworkDamageAgent(attackerGuid, default, default, default);

            byte[] bytes = serializer.Serialize(missionJoinInfo);
            Assert.NotNull(bytes);

            NetworkDamageAgent newEvent = (NetworkDamageAgent)serializer.Deserialize(bytes);

            Assert.Equal(attackerGuid, newEvent.AttackerAgentId);
        }
    }
}
