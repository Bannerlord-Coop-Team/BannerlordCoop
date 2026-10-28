using Autofac;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class BannerSerializationTest
    {
        private const string BannerCode = "11.4.4.1528.1528.764.764.1.0.0.133.5.5.512.512.764.764.0.0.0";

        ILifetimeScope container;
        public BannerSerializationTest()
        {
            container = SerializationTestContainer.BeginScope();
        }

        [Fact]
        public void Banner_Full_Serialization()
        {
            Banner testBanner = new Banner(BannerCode);

            var factory = container.Resolve<IBinaryPackageFactory>();
            BannerBinaryPackage package = new BannerBinaryPackage(testBanner, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<BannerBinaryPackage>(obj);

            BannerBinaryPackage returnedPackage = (BannerBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            Banner newBanner = returnedPackage.Unpack<Banner>(deserializeFactory);

            Assert.Equal(testBanner.Serialize(), newBanner.Serialize());
        }
    }
}
