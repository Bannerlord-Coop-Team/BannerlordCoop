using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class Mat3SerializationTest
    {
        [Fact]
        public void Mat3_Serialize_WithVecs()
        {
            Vec3 v1 = new Vec3(1,2,3);
            Vec3 v2 = new Vec3(4, 5, 6);
            Vec3 v3 = new Vec3(7, 8, 9);
            Mat3 mat3 = new Mat3(v1,v2,v3);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            Mat3BinaryPackage package = new Mat3BinaryPackage(mat3, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Mat3_Full_Serialization_WithVecs()
        {
            Vec3 v1 = new Vec3(1, 2, 3);
            Vec3 v2 = new Vec3(4, 5, 6);
            Vec3 v3 = new Vec3(7, 8, 9);
            Mat3 mat3 = new Mat3(v1, v2, v3);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            Mat3BinaryPackage package = new Mat3BinaryPackage(mat3, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<Mat3BinaryPackage>(obj);

            Mat3BinaryPackage returnedPackage = (Mat3BinaryPackage)obj;

            Mat3 newMat3 = returnedPackage.Unpack<Mat3>();

            Assert.Equal(mat3.s, newMat3.s);
            Assert.Equal(mat3.f, newMat3.f);
            Assert.Equal(mat3.u, newMat3.u);
        }
        [Fact]
        public void Mat3_Serialize_WithCor()
        {
            Mat3 mat3 = new Mat3(1, 2, 3, 4, 5, 6, 7, 8, 9);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            Mat3BinaryPackage package = new Mat3BinaryPackage(mat3, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Mat3_Full_Serialization_WithCor()
        {
            Mat3 mat3 = new Mat3(1, 2, 3, 4, 5, 6, 7, 8, 9);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            Mat3BinaryPackage package = new Mat3BinaryPackage(mat3, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<Mat3BinaryPackage>(obj);

            Mat3BinaryPackage returnedPackage = (Mat3BinaryPackage)obj;

            Mat3 newMat3 = returnedPackage.Unpack<Mat3>();

            Assert.Equal(mat3.s, newMat3.s);
            Assert.Equal(mat3.f, newMat3.f);
            Assert.Equal(mat3.u, newMat3.u);
        }
    }
}
