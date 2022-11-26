using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System.Linq;
using TaleWorlds.Core;
using Xunit;

using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System.Linq;
using TaleWorlds.Core;
using Xunit;
using System.Reflection;
using System;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ItemModifierSerializationTest
    {
        [Fact]
        public void ItemModifier_Serialize()
        {
            ItemModifier ItemModifier = new ItemModifier();

            BinaryPackageFactory factory = new BinaryPackageFactory();
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

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ItemModifierBinaryPackage package = new ItemModifierBinaryPackage(ItemModifier, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ItemModifierBinaryPackage>(obj);

            ItemModifierBinaryPackage returnedPackage = (ItemModifierBinaryPackage)obj;

            ItemModifier newItemModifier = returnedPackage.Unpack<ItemModifier>();

            Assert.Equal(ItemModifier.GetType().GetField("_damage", BindingFlags.Instance | BindingFlags.NonPublic), 
                newItemModifier.GetType().GetField("_damage", BindingFlags.Instance | BindingFlags.NonPublic));

            Assert.Equal(ItemModifier.GetType().GetField("_armor", BindingFlags.Instance | BindingFlags.NonPublic),
                newItemModifier.GetType().GetField("_armor", BindingFlags.Instance | BindingFlags.NonPublic));
        }
    }
}
