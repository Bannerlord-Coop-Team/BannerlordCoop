using GameInterface.Serialization.Dynamic;
using GameInterface.Serialization.Surrogates;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Serialization.Surrogates
{
    public class Vec2SurrogateTests
    {
        [Fact]
        public void NominalTextObjectSurrogate()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.AssignSurrogate<Vec2, Vec2Surrogate>();

            generator.Compile();

            // Verify the type Vec2 can be serialized
            Assert.True(testModel.CanSerialize(typeof(Vec2)));

            Vec2 vec2 = new Vec2(1, 2);

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(vec2);
            Vec2 newVec2 = ser.Deserialize<Vec2>(data);

            Assert.Equal(vec2, newVec2);
        }

        [Fact]
        public void NullTextObjectSurrogate()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.AssignSurrogate<Vec2, Vec2Surrogate>();

            generator.Compile();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);
            Vec2 newVec2 = ser.Deserialize<Vec2>(data);

            Assert.Equal(default, newVec2);
        }
    }
}
