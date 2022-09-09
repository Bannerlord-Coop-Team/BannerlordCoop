using GameInterface.Serialization.Dynamic;
using GameInterface.Serialization.Surrogates;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Serialization.Surrogates
{
    public class Vec3SurrogateTests
    {
        [Fact]
        public void NominalTextObjectSurrogate()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.AssignSurrogate<Vec3, Vec3Surrogate>();

            generator.Compile();

            // Verify the type Vec3 can be serialized
            Assert.True(testModel.CanSerialize(typeof(Vec3)));

            Vec3 vec3 = new Vec3(1, 2, 3, 4);

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(vec3);
            Vec3 newVec3 = ser.Deserialize<Vec3>(data);

            Assert.Equal(vec3, newVec3);
        }

        [Fact]
        public void NullTextObjectSurrogate()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.AssignSurrogate<Vec3, Vec3Surrogate>();

            generator.Compile();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);
            Vec3 newVec3 = ser.Deserialize<Vec3>(data);

            Assert.Equal(default, newVec3);
        }
    }
}
