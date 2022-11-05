using GameInterface.Serialization.Dynamic;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Tests.Serialization.Dynamic
{
    public class SkeletonScaleSerializationTests
    {
        private readonly ITestOutputHelper output;
        public SkeletonScaleSerializationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void NominalSkeletonScaleObjectSerialization()
        {
            var testModel = MakeSkeletonScaleSerializable();

            SkeletonScale skeletonScale = new SkeletonScale();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);

            byte[] data = ser.Serialize(skeletonScale);

            SkeletonScale newSkeletonScale = ser.Deserialize<SkeletonScale>(data);

            Assert.NotNull(newSkeletonScale);
        }

        [Fact]
        public void NullSkeletonScaleObjectSerialization()
        {
            var testModel = MakeSkeletonScaleSerializable();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);

            SkeletonScale newSkeletonScale = ser.Deserialize<SkeletonScale>(data);

            Assert.Null(newSkeletonScale);
        }

        private RuntimeTypeModel MakeSkeletonScaleSerializable()
        {
            string[] excluded = new string[]
            {

            };

            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.CreateDynamicSerializer<SkeletonScale>(excluded);

            generator.AssignSurrogate<Vec3, SurrogateStub<Vec3>>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(SkeletonScale)));

            return testModel;
        }
    }
}
