using Autofac;
using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Services.ObjectManager;
using Missions;
using Missions.Services.Agents.Messages;
using Missions.Services.Network.Data;
using Missions.Services.Network.Messages;
using Missions.Services.Network.Surrogates;
using ProtoBuf;
using ProtoBuf.Meta;
using ProtoBuf.Serializers;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

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

            ContainerProvider.SetContainer(builder.Build());
        }

        /// <summary>
        /// Registers a surrogate unless the type is already handled by the (process-wide) default
        /// runtime model. Mirrors SurrogateCollection.AddSurrogate: once any serializer has been
        /// generated for a type, protobuf-net freezes it and a repeated SetSurrogate throws.
        /// </summary>
        private static void TrySetSurrogate<T, TSurrogate>()
        {
            if (RuntimeTypeModel.Default.CanSerialize(typeof(T))) return;

            RuntimeTypeModel.Default.SetSurrogate<T, TSurrogate>();
        }

        [Fact]
        public void Serialize_Test()
        {
            TrySetSurrogate<Vec3, Vec3Surrogate>();
            TrySetSurrogate<Vec2, Vec2Surrogate>();
            TrySetSurrogate<CharacterObject, CharacterObjectSurrogate>();
            TrySetSurrogate<Equipment, EquipmentSurrogate>();

            var character = (CharacterObject)FormatterServices.GetUninitializedObject(typeof(CharacterObject));

            character.StringId = "Test Character";

            NetworkMissionJoinInfo missionJoinInfo = new NetworkMissionJoinInfo( 
                character, 
                default,
                default,
                default,
                default,
                Array.Empty<AiAgentData>());

            byte[] bytes = serializer.Serialize(missionJoinInfo);

            Assert.NotNull(bytes);

            NetworkMissionJoinInfo newEvent = (NetworkMissionJoinInfo)serializer.Deserialize(bytes);

            Assert.Equal(character.StringId, newEvent.CharacterObject.StringId);
        }

        [Fact]
        public void Serialize2_Test()
        {
            TrySetSurrogate<Vec3, Vec3Surrogate>();
            TrySetSurrogate<Vec2, Vec2Surrogate>();
            TrySetSurrogate<CharacterObject, CharacterObjectSurrogate>();
            TrySetSurrogate<AttackCollisionData, AttackCollisionDataSurrogate>();
            TrySetSurrogate<Blow, BlowSurrogate>();

            var attackerGuid = Guid.NewGuid();

            NetworkDamageAgent missionJoinInfo = new NetworkDamageAgent(attackerGuid, default, default, default);

            byte[] bytes = serializer.Serialize(missionJoinInfo);
            Assert.NotNull(bytes);

            NetworkDamageAgent newEvent = (NetworkDamageAgent)serializer.Deserialize(bytes);

            Assert.Equal(attackerGuid, newEvent.AttackerAgentId);
        }
    }
}
