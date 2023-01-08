using GameInterface.Serialization;
using GameInterface.Serialization.External;
using Xunit;
using TaleWorlds.Library;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class MatrixFrameSerializationTest
    {
        [Fact]
        public void MatrixFrame_Serialize()
        {
            MatrixFrame matrixFrame = new MatrixFrame();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            MatrixFrameBinaryPackage package = new MatrixFrameBinaryPackage(matrixFrame, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void MatrixFrame_Full_Serialization_Whole()
        {
            MatrixFrame matrixFrame = new MatrixFrame(new Mat3(1,2,3,4,5,6,7,8,9),new Vec3(1,2,3));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            MatrixFrameBinaryPackage package = new MatrixFrameBinaryPackage(matrixFrame, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<MatrixFrameBinaryPackage>(obj);

            MatrixFrameBinaryPackage returnedPackage = (MatrixFrameBinaryPackage)obj;

            MatrixFrame newMatrixFrame = returnedPackage.Unpack<MatrixFrame>();

            Assert.Equal(matrixFrame, newMatrixFrame);
            Assert.Equal(matrixFrame.origin, newMatrixFrame.origin);
            Assert.Equal(matrixFrame.rotation, newMatrixFrame.rotation);

        }

        [Fact]
        public void MatrixFrame_Full_Serialization_Raw1()
        {
            MatrixFrame matrixFrame = new MatrixFrame(1,2,3,4,5,6,7,8,9,10,11,12);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            MatrixFrameBinaryPackage package = new MatrixFrameBinaryPackage(matrixFrame, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<MatrixFrameBinaryPackage>(obj);

            MatrixFrameBinaryPackage returnedPackage = (MatrixFrameBinaryPackage)obj;

            MatrixFrame newMatrixFrame = returnedPackage.Unpack<MatrixFrame>();

            Assert.True(matrixFrame.Equals(newMatrixFrame));
            Assert.Equal(matrixFrame.origin, newMatrixFrame.origin);
            Assert.Equal(matrixFrame.rotation, newMatrixFrame.rotation);

        }

        [Fact]
        public void MatrixFrame_Full_Serialization_Raw2()
        {
            MatrixFrame matrixFrame = new MatrixFrame(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,13,14,15,16);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            MatrixFrameBinaryPackage package = new MatrixFrameBinaryPackage(matrixFrame, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<MatrixFrameBinaryPackage>(obj);

            MatrixFrameBinaryPackage returnedPackage = (MatrixFrameBinaryPackage)obj;

            MatrixFrame newMatrixFrame = returnedPackage.Unpack<MatrixFrame>();

            Assert.True(matrixFrame.Equals(newMatrixFrame));
            Assert.Equal(matrixFrame.origin, newMatrixFrame.origin);
            Assert.Equal(matrixFrame.rotation, newMatrixFrame.rotation);

        }
    }
}
