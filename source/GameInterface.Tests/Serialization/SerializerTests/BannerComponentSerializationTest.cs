using Autofac;
using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class BannerComponentSerializationTest
    {
        IContainer container;
        public BannerComponentSerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void BannerComponent_Serialize()
        {
            ItemObject itemObject = new ItemObject("Attached Item");
            BannerComponent BannerComponent = new BannerComponent(itemObject);

            foreach (var property in typeof(BannerComponent).GetProperties())
            {
                property.SetRandom(BannerComponent);
            }

            var factory = container.Resolve<IBinaryPackageFactory>();
            BannerComponentBinaryPackage package = new BannerComponentBinaryPackage(BannerComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void BannerComponent_Full_Serialization()
        {
            ItemObject itemObject = new ItemObject("Attached Item");
            BannerComponent BannerComponent = new BannerComponent(itemObject);
            var objectManager = container.Resolve<IObjectManager>();

            objectManager.AddExisting(itemObject.StringId, itemObject);

            foreach (var property in typeof(BannerComponent).GetProperties())
            {
                property.SetRandom(BannerComponent);
            }

            // Set banner effect and register it with the object manager
            BannerEffect effect = new BannerEffect("myEffect");
            Assert.True(objectManager.AddExisting(effect.StringId, effect));

            BannerComponent.BannerEffect = effect;

            // Setup binary package with dependencies 
            var factory = container.Resolve<IBinaryPackageFactory>();
            BannerComponentBinaryPackage package = new BannerComponentBinaryPackage(BannerComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<BannerComponentBinaryPackage>(obj);

            BannerComponentBinaryPackage returnedPackage = (BannerComponentBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            BannerComponent newBannerComponent = returnedPackage.Unpack<BannerComponent>(deserializeFactory);

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

            Assert.Equal(BannerComponent.BannerEffect, newBannerComponent.BannerEffect);
        }
    }
}
