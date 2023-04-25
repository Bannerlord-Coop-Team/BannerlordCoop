using Autofac;
using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Stubs;
using Missions;
using Missions.Messages;
using Missions.Services.Agents.Messages;
using Missions.Services.Network.Data;
using Missions.Services.Network.Surrogates;
using ProtoBuf.Meta;
using System;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace IntroductionServerTests
{
    public class NetworkMessageSerializationTest
    {
        public NetworkMessageSerializationTest()
        {
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

            byte[] bytes = ProtoBufSerializer.Serialize(missionJoinInfo);

            Assert.NotNull(bytes);

            NetworkMissionJoinInfo newEvent = (NetworkMissionJoinInfo)ProtoBufSerializer.Deserialize(bytes);

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

            NetworkAgentDamaged missionJoinInfo = new NetworkAgentDamaged(attackerGuid, default, default, default);

            byte[] bytes = ProtoBufSerializer.Serialize(missionJoinInfo);

            Assert.NotNull(bytes);

            NetworkAgentDamaged newEvent = (NetworkAgentDamaged)ProtoBufSerializer.Deserialize(bytes);

            Assert.Equal(attackerGuid, newEvent.AttackerAgentId);
        }
    }
}
