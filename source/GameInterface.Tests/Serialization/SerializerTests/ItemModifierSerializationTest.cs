using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Tests.Bootstrap;
using System.Reflection;
using TaleWorlds.Core;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ItemModifierSerializationTest
    {
        IContainer container;
        public ItemModifierSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void ItemModifier_Serialize()
        {
            ItemModifier ItemModifier = new ItemModifier();

            var factory = container.Resolve<IBinaryPackageFactory>();
            ItemModifierBinaryPackage package = new ItemModifierBinaryPackage(ItemModifier, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
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

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

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
