using Autofac;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ItemModifierSerializationTest
    {
        ILifetimeScope container;
        public ItemModifierSerializationTest()
        {
            container = SerializationTestContainer.BeginScope();
        }

        [Fact]
        public void ItemModifier_Full_Serialization()
        {
            ItemModifier ItemModifier = new ItemModifier();
            ItemModifier.ModifyDamage(10);
            ItemModifier.ModifyArmor(15);

            var factory = container.Resolve<IBinaryPackageFactory>();
            ItemModifierBinaryPackage package = new ItemModifierBinaryPackage(ItemModifier, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<ItemModifierBinaryPackage>(obj);

            ItemModifierBinaryPackage returnedPackage = (ItemModifierBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            ItemModifier newItemModifier = returnedPackage.Unpack<ItemModifier>(deserializeFactory);

            Assert.Equal(ItemModifier.Damage,
                         newItemModifier.Damage);

            Assert.Equal(ItemModifier.Armor,
                         newItemModifier.Armor);
        }
    }
}
