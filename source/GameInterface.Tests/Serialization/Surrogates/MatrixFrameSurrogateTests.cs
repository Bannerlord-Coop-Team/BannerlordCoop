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
    public class MatrixFrameSurrogateTests
    {
        [Fact]
        public void NominalTextObjectSurrogate()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.AssignSurrogate<MatrixFrame, MatrixFrameSurrogate>();
            generator.AssignSurrogate<Mat3, Mat3Surrogate>();
            generator.AssignSurrogate<Vec3, Vec3Surrogate>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(MatrixFrame)));

            Mat3 mat3 = new Mat3(
                new Vec3(0, 1, 2),
                new Vec3(0, 1, 2),
                new Vec3(0, 1, 2)
            );

            MatrixFrame matFrame = new MatrixFrame(mat3, new Vec3(3, 4, 5));

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(matFrame);
            MatrixFrame newMatrixFrame = ser.Deserialize<MatrixFrame>(data);

            Assert.Equal(matFrame, newMatrixFrame);
        }

        [Fact]
        public void NullTextObjectSurrogate()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.AssignSurrogate<MatrixFrame, MatrixFrameSurrogate>();
            generator.AssignSurrogate<Mat3, Mat3Surrogate>();
            generator.AssignSurrogate<Vec3, Vec3Surrogate>();

            generator.Compile();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);
            MatrixFrame newMatrixFrame = ser.Deserialize<MatrixFrame>(data);

            Assert.Equal(default, newMatrixFrame);
        }
    }
}
