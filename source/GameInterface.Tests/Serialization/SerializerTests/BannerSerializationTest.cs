using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Tests.Bootstrap;
using System.Linq;
using TaleWorlds.Core;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class BannerSerializationTest
    {
        IContainer container;
        public BannerSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void Banner_Serialize()
        {
            Banner testBanner = new Banner();

            var factory = container.Resolve<IBinaryPackageFactory>();
            BannerBinaryPackage package = new BannerBinaryPackage(testBanner, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Banner_Full_Serialization()
        {
            Banner testBanner = new Banner();

            var factory = container.Resolve<IBinaryPackageFactory>();
            BannerBinaryPackage package = new BannerBinaryPackage(testBanner, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<BannerBinaryPackage>(obj);

            BannerBinaryPackage returnedPackage = (BannerBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            Banner newBanner = returnedPackage.Unpack<Banner>(deserializeFactory);

            foreach (var data in testBanner.BannerDataList.Zip(newBanner.BannerDataList, (bannerData, newBannerData) => new { bannerData, newBannerData }))
            {
                Assert.Equal(data.bannerData, data.newBannerData);
            }
        }
    }
}
