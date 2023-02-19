using Common.PacketHandlers;
using Common.Serialization;
using Missions.Messages;
using Missions.Services.Network.Surrogates;
using ProtoBuf.Meta;
using System;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Xunit;

namespace IntroductionServerTests
{
    public class NetworkMessageSerializationTest
    {
        [Fact]
        public void Serialize_Test()
        {
            RuntimeTypeModel.Default.SetSurrogate<Vec3, Vec3Surrogate>();
            RuntimeTypeModel.Default.SetSurrogate<Vec2, Vec2Surrogate>();

            var character = (CharacterObject)FormatterServices.GetUninitializedObject(typeof(CharacterObject));
            NetworkMissionJoinInfo missionJoinInfo = new NetworkMissionJoinInfo(character, false, default(Guid), default(Vec3), default(Guid[]), default(Vec3[]), Array.Empty<string>());
            EventPacket eventPacket = new EventPacket(missionJoinInfo);
            byte[] bytes = ProtoBufSerializer.Serialize(eventPacket);

            Assert.NotNull(bytes);

            NetworkMissionJoinInfo newEvent = (NetworkMissionJoinInfo)eventPacket.Event;
        }
    }
}
