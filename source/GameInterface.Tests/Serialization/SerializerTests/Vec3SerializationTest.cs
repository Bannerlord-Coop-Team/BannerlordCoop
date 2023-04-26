using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Tests.Bootstrap;
using TaleWorlds.Library;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class Vec3SerializationTest
    {
        IContainer container;
        public Vec3SerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void Vec3_Serialize()
        {
            Vec3 vec3 = new Vec3(1,2,3);

            var factory = container.Resolve<IBinaryPackageFactory>();
            Vec3BinaryPackage package = new Vec3BinaryPackage(vec3, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Vec3_Full_Serialization()
        {
            Vec3 vec3 = new Vec3(1, 2, 3, 4);

            var factory = container.Resolve<IBinaryPackageFactory>();
            Vec3BinaryPackage package = new Vec3BinaryPackage(vec3, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

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
