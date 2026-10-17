using Autofac;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class HeroDeveloperSerializationTest
    {
        ILifetimeScope container;
        public HeroDeveloperSerializationTest()
        {
            GameBootStrap.Initialize();

            container = SerializationTestContainer.BeginScope();
        }

        [Fact]
        public void HeroDeveloper_Full_Serialization()
        {
            Hero hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            var objectManager = container.Resolve<IObjectManager>();

            hero.StringId = "myHero";

            objectManager.AddExisting(hero.StringId, hero);

            // Setup instance and fields
            HeroDeveloper HeroDeveloper = (HeroDeveloper)FormatterServices.GetUninitializedObject(typeof(HeroDeveloper));

            HeroDeveloper._totalXp = 101;
            HeroDeveloper.Hero = hero;
            HeroDeveloper.UnspentFocusPoints = 54;
            HeroDeveloper.UnspentAttributePoints = 68;

            var factory = container.Resolve<IBinaryPackageFactory>();
            HeroDeveloperBinaryPackage package = new HeroDeveloperBinaryPackage(HeroDeveloper, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<HeroDeveloperBinaryPackage>(obj);

            HeroDeveloperBinaryPackage returnedPackage = (HeroDeveloperBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            HeroDeveloper newHeroDeveloper = returnedPackage.Unpack<HeroDeveloper>(deserializeFactory);

            Assert.Equal(HeroDeveloper.TotalXp, newHeroDeveloper.TotalXp);
            Assert.Equal(HeroDeveloper.UnspentFocusPoints, newHeroDeveloper.UnspentFocusPoints);
            Assert.Equal(HeroDeveloper.UnspentAttributePoints, newHeroDeveloper.UnspentAttributePoints);
            Assert.Equal(HeroDeveloper.Hero, newHeroDeveloper.Hero);
        }
    }
}
