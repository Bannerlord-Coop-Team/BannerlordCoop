using Autofac;
using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Stubs;
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
            builder.RegisterType<ObjectManagerAdapterStub>().As<IObjectManager>().InstancePerLifetimeScope();
            builder.RegisterType<BinaryPackageFactory>().As<IBinaryPackageFactory>().AutoActivate().SingleInstance();

            ContainerProvider.SetContainer(builder.Build());
        }

        [Fact]
        public void Serialize_Test()
        {
            RuntimeTypeModel.Default.SetSurrogate<Vec3, Vec3Surrogate>();
            RuntimeTypeModel.Default.SetSurrogate<Vec2, Vec2Surrogate>();
            RuntimeTypeModel.Default.SetSurrogate<CharacterObject, CharacterObjectSurrogate>();
            RuntimeTypeModel.Default.SetSurrogate<Equipment, EquipmentSurrogate>();

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
            RuntimeTypeModel.Default.SetSurrogate<Vec3, Vec3Surrogate>();
            RuntimeTypeModel.Default.SetSurrogate<Vec2, Vec2Surrogate>();
            RuntimeTypeModel.Default.SetSurrogate<CharacterObject, CharacterObjectSurrogate>();
            RuntimeTypeModel.Default.SetSurrogate<AttackCollisionData, AttackCollisionDataSurrogate>();
            RuntimeTypeModel.Default.SetSurrogate<Blow, BlowSurrogate>();

            var attackerGuid = Guid.NewGuid();

            NetworkDamageAgent missionJoinInfo = new NetworkDamageAgent(attackerGuid, default, default, default);

            byte[] bytes = serializer.Serialize(missionJoinInfo);
            Assert.NotNull(bytes);

            NetworkDamageAgent newEvent = (NetworkDamageAgent)serializer.Deserialize(bytes);

            Assert.Equal(attackerGuid, newEvent.AttackerAgentId);
        }
    }
}
