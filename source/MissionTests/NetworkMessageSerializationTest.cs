using Common.Serialization;
using Missions.Messages;
using Missions.Services.Network.PacketHandlers;
using Missions.Services.Network.Surrogates;
using ProtoBuf.Meta;
using System;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Xunit;

<<<<<<< HEAD
namespace IntroductionServerTests
=======
namespace MissionTests
>>>>>>> NetworkEvent-refactor
{
    public class NetworkMessageSerializationTest
    {
        [Fact]
        public void Serialize_Test()
        {
            RuntimeTypeModel.Default.SetSurrogate<Vec3, Vec3Surrogate>();
            RuntimeTypeModel.Default.SetSurrogate<Vec2, Vec2Surrogate>();

            var character = (CharacterObject)FormatterServices.GetUninitializedObject(typeof(CharacterObject));
            MissionJoinInfo missionJoinInfo = new MissionJoinInfo(character, default(Guid), default(Vec3));
            EventPacket eventPacket = new EventPacket(missionJoinInfo);
            byte[] bytes = ProtoBufSerializer.Serialize(eventPacket);

            Assert.NotNull(bytes);

            MissionJoinInfo newEvent = (MissionJoinInfo)eventPacket.Event;
        }
    }
}
