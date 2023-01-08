using GameInterface.Serialization;
using GameInterface.Serialization.External;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class Vec2SerializationTest
    {
        [Fact]
        public void Vec2_Serialize()
        {
            Vec2 vec2 = new Vec2(1.1f,2.2f);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            Vec2BinaryPackage package = new Vec2BinaryPackage(vec2, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Vec2_Full_Serialization()
        {
            Vec2 vec2 = new Vec2(1.1f, 2.2f);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            Vec2BinaryPackage package = new Vec2BinaryPackage(vec2, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<Vec2BinaryPackage>(obj);

            Vec2BinaryPackage returnedPackage = (Vec2BinaryPackage)obj;

            Vec2 newVec2 = returnedPackage.Unpack<Vec2>();

            Assert.Equal(vec2.X, newVec2.X);
            Assert.Equal(vec2.Y, newVec2.Y);
        }
    }
}
