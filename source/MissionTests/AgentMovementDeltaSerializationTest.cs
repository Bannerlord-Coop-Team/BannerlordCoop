using Common.Serialization;
using Missions.Services.Agents.Messages;
using Missions.Services.Agents.Packets;
using Missions.Services.Network.Surrogates;
using ProtoBuf.Meta;
using System;
using TaleWorlds.Library;
using Xunit;

namespace IntroductionServerTests
{
    public class AgentMovementDeltaSerializationTest
    {
        [Fact]
        public void Serialize_Test()
        {
            try
            {
                RuntimeTypeModel.Default.SetSurrogate<Vec3, Vec3Surrogate>();
                RuntimeTypeModel.Default.SetSurrogate<Vec2, Vec2Surrogate>();
            }
            catch
            {
                // nop
            }

            var delta = new AgentMovement(Guid.NewGuid());

            var bytes = ProtoBufSerializer.Serialize(delta);

            Assert.NotNull(bytes);
            Assert.NotEmpty(bytes);

            var desDelta = (AgentMovement)ProtoBufSerializer.Deserialize(bytes);

            Assert.Equal(delta, desDelta);
        }
    }
}
