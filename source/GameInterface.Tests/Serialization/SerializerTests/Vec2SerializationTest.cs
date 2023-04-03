using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class Vec2SerializationTest
    {
        IContainer container;
        public Vec2SerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void Vec2_Serialize()
        {
            Vec2 vec2 = new Vec2(1.1f,2.2f);

            var factory = container.Resolve<IBinaryPackageFactory>();
            Vec2BinaryPackage package = new Vec2BinaryPackage(vec2, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Vec2_Full_Serialization()
        {
            Vec2 vec2 = new Vec2(1.1f, 2.2f);

            var factory = container.Resolve<IBinaryPackageFactory>();
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
