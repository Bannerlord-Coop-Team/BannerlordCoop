using GameInterface.Serialization;
using GameInterface.Serialization.External;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class Vec3SerializationTest
    {
        [Fact]
        public void Vec3_Serialize()
        {
            Vec3 vec3 = new Vec3(1,2,3);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            Vec3BinaryPackage package = new Vec3BinaryPackage(vec3, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Vec3_Full_Serialization()
        {
            Vec3 vec3 = new Vec3(1, 2, 3, 4);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            Vec3BinaryPackage package = new Vec3BinaryPackage(vec3, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<Vec3BinaryPackage>(obj);

            Vec3BinaryPackage returnedPackage = (Vec3BinaryPackage)obj;

            Vec3 newVec3 = returnedPackage.Unpack<Vec3>();

            Assert.Equal(vec3.X, newVec3.X);
            Assert.Equal(vec3.Y, newVec3.Y);
            Assert.Equal(vec3.Z, newVec3.Z);
            Assert.Equal(vec3.w, newVec3.w);
        }
    }
}
