using GameInterface.Serialization;
using GameInterface.Serialization.External;
using System.Linq;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class BannerSerializationTest
    {
        [Fact]
        public void Banner_Serialize()
        {
            Banner testBanner = new Banner();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            BannerBinaryPackage package = new BannerBinaryPackage(testBanner, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Banner_Full_Serialization()
        {
            Banner testBanner = new Banner();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            BannerBinaryPackage package = new BannerBinaryPackage(testBanner, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<BannerBinaryPackage>(obj);

            BannerBinaryPackage returnedPackage = (BannerBinaryPackage)obj;

            Banner newBanner = returnedPackage.Unpack<Banner>();

            foreach (var data in testBanner.BannerDataList.Zip(newBanner.BannerDataList, (bannerData, newBannerData) => new { bannerData, newBannerData }))
            {
                Assert.Equal(data.bannerData, data.newBannerData);
            }
        }
    }
}
