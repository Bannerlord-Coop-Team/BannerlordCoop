using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class BannerComponentSerializationTest
    {
        public BannerComponentSerializationTest()
        {
            GameBootStrap.Initialize();
        }

        [Fact]
        public void BannerComponent_Serialize()
        {
            ItemObject itemObject = MBObjectManager.Instance.CreateObject<ItemObject>();
            BannerComponent BannerComponent = new BannerComponent(itemObject);

            foreach (var property in typeof(BannerComponent).GetProperties())
            {
                property.SetRandom(BannerComponent);
            }

            BinaryPackageFactory factory = new BinaryPackageFactory();
            BannerComponentBinaryPackage package = new BannerComponentBinaryPackage(BannerComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly PropertyInfo BannerEffect = typeof(BannerComponent).GetProperty(nameof(BannerComponent.BannerEffect));
        [Fact]
        public void BannerComponent_Full_Serialization()
        {
            ItemObject itemObject = MBObjectManager.Instance.CreateObject<ItemObject>();
            BannerComponent BannerComponent = new BannerComponent(itemObject);

            foreach (var property in typeof(BannerComponent).GetProperties())
            {
                property.SetRandom(BannerComponent);
            }

            // Set banner effect and register it with the MBObject manager
            BannerEffect effect = new BannerEffect("myEffect");
            MBObjectManager.Instance.RegisterObject(effect);

            BannerEffect.SetValue(BannerComponent, effect);

            // Setup binary package with dependencies 
            BinaryPackageFactory factory = new BinaryPackageFactory();
            BannerComponentBinaryPackage package = new BannerComponentBinaryPackage(BannerComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<BannerComponentBinaryPackage>(obj);

            BannerComponentBinaryPackage returnedPackage = (BannerComponentBinaryPackage)obj;

            BannerComponent newBannerComponent = returnedPackage.Unpack<BannerComponent>();

            HashSet<string> excludes = new HashSet<string>
            {
                "BannerEffect",
                "PrimaryWeapon",
            };

            var properties = typeof(BannerComponent).GetProperties().Where(p => excludes.Contains(p.Name) == false);

            foreach (var property in properties)
            {
                Assert.Equal(property.GetValue(BannerComponent), property.GetValue(newBannerComponent));
            }

            Assert.Equal(BannerEffect.GetValue(BannerComponent), BannerEffect.GetValue(newBannerComponent));
        }
    }
}
