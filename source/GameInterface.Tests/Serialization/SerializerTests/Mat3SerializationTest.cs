using GameInterface.Serialization;
using GameInterface.Serialization.External;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class Mat3SerializationTest
    {
        [Fact]
        public void Mat3_Serialize()
        {
            Mat3 Mat3 = new Mat3(new Vec3(1,2,3), new Vec3(4,5,6), new Vec3(7,8,9));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            Mat3BinaryPackage package = new Mat3BinaryPackage(Mat3, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Mat3_Full_Serialization()
        {
            Mat3 Mat3 = new Mat3(new Vec3(11, 2, 3), new Vec3(4, 5, 6), new Vec3(7, 8, 0));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            Mat3BinaryPackage package = new Mat3BinaryPackage(Mat3, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<Mat3BinaryPackage>(obj);

            Mat3BinaryPackage returnedPackage = (Mat3BinaryPackage)obj;

            Mat3 newMat3 = returnedPackage.Unpack<Mat3>();

            Assert.Equal(Mat3.f, newMat3.f);
            Assert.Equal(Mat3.s, newMat3.s);
            Assert.Equal(Mat3.u, newMat3.u);
        }
    }
}
