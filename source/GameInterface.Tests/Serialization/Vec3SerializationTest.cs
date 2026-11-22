using Autofac;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class Vec3SerializationTest
    {
        ILifetimeScope container;
        public Vec3SerializationTest()
        {
            container = SerializationTestContainer.BeginScope();
        }

        [Fact]
        public void Vec3_Full_Serialization()
        {
            Vec3 vec3 = new Vec3(1, 2, 3, 4);

            var factory = container.Resolve<IBinaryPackageFactory>();
            Vec3BinaryPackage package = new Vec3BinaryPackage(vec3, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<Vec3BinaryPackage>(obj);

            Vec3BinaryPackage returnedPackage = (Vec3BinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            Vec3 newVec3 = returnedPackage.Unpack<Vec3>(deserializeFactory);

            Assert.Equal(vec3.X, newVec3.X);
            Assert.Equal(vec3.Y, newVec3.Y);
            Assert.Equal(vec3.Z, newVec3.Z);
            Assert.Equal(vec3.w, newVec3.w);
        }
    }
}
