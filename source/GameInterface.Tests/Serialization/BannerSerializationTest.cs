using Autofac;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using TaleWorlds.Core;
using TaleWorlds.Library;
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

        [Fact]
        public void Banner_WithNoLayers_UnpacksToDefaultBanner()
        {
            // The default banner takes its colours from the palette, which is empty outside the game.
            BannerManager.Initialize();
            if (BannerManager.Instance.ReadOnlyColorPalette.Count == 0)
            {
                BannerManager.Instance._colorPalette[9101] = new BannerColor(0xFF102030, true, true);
                BannerManager.Instance.ReadOnlyColorPalette = BannerManager.Instance._colorPalette.GetReadOnlyDictionary();
            }

            var factory = container.Resolve<IBinaryPackageFactory>();
            BannerBinaryPackage package = new BannerBinaryPackage(new Banner(), factory);

            package.Pack();

            var returnedPackage = (BannerBinaryPackage)BinaryPackageSerializer.Deserialize(BinaryPackageSerializer.Serialize(package));
            Banner newBanner = returnedPackage.Unpack<Banner>(container.Resolve<IBinaryPackageFactory>());

            Assert.False(newBanner.IsBannerDataListEmpty());
        }
    }
}
