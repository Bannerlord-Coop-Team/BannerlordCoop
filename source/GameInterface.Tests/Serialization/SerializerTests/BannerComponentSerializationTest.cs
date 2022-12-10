using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Xunit;
using Xunit.Abstractions;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class BannerComponentSerializationTest
    {
        public BannerComponentSerializationTest()
        {
            MBObjectManager.Init();
            MBObjectManager.Instance.RegisterType<ItemObject>("Item", "Items", 4U, true, false);
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

            foreach (var property in typeof(BannerComponent).GetProperties())
            {
                property.SetRandom(BannerComponent);
            }
        }
    }
}
