using GameInterface.Serialization.External;
using GameInterface.Serialization;
using TaleWorlds.Core;
using Xunit;
using System.Runtime.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class BannerEffectSerializationTest
    {
        [Fact]
        public void BannerEffect_Serialize()
        {
            BannerEffect testBannerEffect = (BannerEffect)FormatterServices.GetUninitializedObject(typeof(BannerEffect));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            BannerEffectBinaryPackage package = new BannerEffectBinaryPackage(testBannerEffect, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void BannerEffect_Full_Serialization()
        {
            BannerEffect testBannerEffect = (BannerEffect)FormatterServices.GetUninitializedObject(typeof(BannerEffect));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            BannerEffectBinaryPackage package = new BannerEffectBinaryPackage(testBannerEffect, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<BannerEffectBinaryPackage>(obj);

            BannerEffectBinaryPackage returnedPackage = (BannerEffectBinaryPackage)obj;

            Assert.Equal(returnedPackage.stringId, package.stringId);
        }
    }
}
