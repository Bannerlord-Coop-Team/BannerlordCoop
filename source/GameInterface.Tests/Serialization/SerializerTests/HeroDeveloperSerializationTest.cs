using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class HeroDeveloperSerializationTest
    {
        IContainer container;
        public HeroDeveloperSerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        private readonly static FieldInfo _totalXp = typeof(HeroDeveloper).GetField("_totalXp", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly static PropertyInfo Hero = typeof(HeroDeveloper).GetProperty(nameof(HeroDeveloper.Hero));
        private readonly static PropertyInfo UnspentFocusPoints = typeof(HeroDeveloper).GetProperty(nameof(HeroDeveloper.UnspentFocusPoints));
        private readonly static PropertyInfo UnspentAttributePoints = typeof(HeroDeveloper).GetProperty(nameof(HeroDeveloper.UnspentAttributePoints));
        [Fact]
        public void HeroDeveloper_Serialize()
        {
            HeroDeveloper HeroDeveloper = (HeroDeveloper)FormatterServices.GetUninitializedObject(typeof(HeroDeveloper));

            var factory = container.Resolve<IBinaryPackageFactory>();
            HeroDeveloperBinaryPackage package = new HeroDeveloperBinaryPackage(HeroDeveloper, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
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

            _totalXp.SetValue(HeroDeveloper, 101);
            Hero.SetValue(HeroDeveloper, hero);
            UnspentFocusPoints.SetValue(HeroDeveloper, 54);
            UnspentAttributePoints.SetValue(HeroDeveloper, 68);

            var factory = container.Resolve<IBinaryPackageFactory>();
            HeroDeveloperBinaryPackage package = new HeroDeveloperBinaryPackage(HeroDeveloper, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

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
