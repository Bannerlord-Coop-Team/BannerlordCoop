using GameInterface.Serialization.Dynamic;
using GameInterface.Serialization.Surrogates;
using ProtoBuf.Meta;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Serialization.Surrogates
{
    public class Mat3SurrogateTests
    {
        [Fact]
        public void NominalTextObjectSurrogate()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.AssignSurrogate<Mat3, Mat3Surrogate>();
            generator.AssignSurrogate<Vec3, Vec3Surrogate>();

            generator.Compile();

            // Verify the type Mat3 can be serialized
            Assert.True(testModel.CanSerialize(typeof(Mat3)));

            Mat3 mat3 = new Mat3(
                new Vec3(0, 1, 2),
                new Vec3(0, 1, 2),
                new Vec3(0, 1, 2)
            );

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(mat3);
            Mat3 newMat3 = ser.Deserialize<Mat3>(data);

            Assert.Equal(mat3, newMat3);
        }

        [Fact]
        public void NullTextObjectSurrogate()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.AssignSurrogate<Mat3, Mat3Surrogate>();
            generator.AssignSurrogate<Vec3, Vec3Surrogate>();

            generator.Compile();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);
            Mat3 newMat3 = ser.Deserialize<Mat3>(data);

            Assert.Equal(default, newMat3);
        }
    }
}
