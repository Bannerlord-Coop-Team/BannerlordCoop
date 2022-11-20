using GameInterface.Serialization.Impl;
using GameInterface.Serialization;
using TaleWorlds.Core;
using Xunit;
using System.Linq;
using TaleWorlds.CampaignSystem;
using System.Runtime.Serialization;
using Common.Extensions;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class HeroSerializationTest
    {
        [Fact]
        public void Hero_Serialize()
        {
            Hero testHero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            HeroBinaryPackage package = new HeroBinaryPackage(testHero, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Hero_Full_Serialization()
        {
            Hero testHero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            HeroBinaryPackage package = new HeroBinaryPackage(testHero, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<HeroBinaryPackage>(obj);

            HeroBinaryPackage returnedPackage = (HeroBinaryPackage)obj;

            Hero newHero = returnedPackage.Unpack<Hero>();

            // TODO verify values
            Assert.Fail("Finish test");
        }
    }
}
