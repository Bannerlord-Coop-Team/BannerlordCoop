using Autofac;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class MatrixFrameSerializationTest
    {
        ILifetimeScope container;
        public MatrixFrameSerializationTest()
        {
            container = SerializationTestContainer.BeginScope();
        }

        [Fact]
        public void MatrixFrame_Full_Serialization_Whole()
        {
            MatrixFrame matrixFrame = new MatrixFrame(new Mat3(1,2,3,4,5,6,7,8,9),new Vec3(1,2,3));

            var factory = container.Resolve<IBinaryPackageFactory>();
            MatrixFrameBinaryPackage package = new MatrixFrameBinaryPackage(matrixFrame, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<MatrixFrameBinaryPackage>(obj);

            MatrixFrameBinaryPackage returnedPackage = (MatrixFrameBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            MatrixFrame newMatrixFrame = returnedPackage.Unpack<MatrixFrame>(deserializeFactory);

            Assert.Equal(matrixFrame, newMatrixFrame);
            Assert.Equal(matrixFrame.origin, newMatrixFrame.origin);
            Assert.Equal(matrixFrame.rotation, newMatrixFrame.rotation);
        }

        [Fact]
        public void MatrixFrame_Full_Serialization_Raw1()
        {
            MatrixFrame matrixFrame = new MatrixFrame(1,2,3,4,5,6,7,8,9,10,11,12);

            var factory = container.Resolve<IBinaryPackageFactory>();
            MatrixFrameBinaryPackage package = new MatrixFrameBinaryPackage(matrixFrame, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<MatrixFrameBinaryPackage>(obj);

            MatrixFrameBinaryPackage returnedPackage = (MatrixFrameBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            MatrixFrame newMatrixFrame = returnedPackage.Unpack<MatrixFrame>(deserializeFactory);

            Assert.True(matrixFrame.Equals(newMatrixFrame));
            Assert.Equal(matrixFrame.origin, newMatrixFrame.origin);
            Assert.Equal(matrixFrame.rotation, newMatrixFrame.rotation);
        }

        [Fact]
        public void MatrixFrame_Full_Serialization_Raw2()
        {
            MatrixFrame matrixFrame = new MatrixFrame(
                1, 2, 3, 4, 5, 
                6, 7, 8, 9, 10, 
                11, 12, 13, 14, 15, 16);

            var factory = container.Resolve<IBinaryPackageFactory>();
            MatrixFrameBinaryPackage package = new MatrixFrameBinaryPackage(matrixFrame, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<MatrixFrameBinaryPackage>(obj);

            MatrixFrameBinaryPackage returnedPackage = (MatrixFrameBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            MatrixFrame newMatrixFrame = returnedPackage.Unpack<MatrixFrame>(deserializeFactory);

            Assert.True(matrixFrame.Equals(newMatrixFrame));
            Assert.Equal(matrixFrame.origin, newMatrixFrame.origin);
            Assert.Equal(matrixFrame.rotation, newMatrixFrame.rotation);
        }
    }
}
