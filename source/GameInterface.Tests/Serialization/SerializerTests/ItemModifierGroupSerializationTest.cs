using GameInterface.Serialization;
using GameInterface.Serialization.External;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ItemModifierGroupSerializationTest
    {
        [Fact]
        public void ItemModifierGroup_Serialize()
        {
            ItemModifierGroup ItemModifierGroup = new ItemModifierGroup();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ItemModifierGroupBinaryPackage package = new ItemModifierGroupBinaryPackage(ItemModifierGroup, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly FieldInfo _itemModifiers = typeof(ItemModifierGroup).GetField("_itemModifiers", BindingFlags.NonPublic | BindingFlags.Instance);
        [Fact]
        public void ItemModifierGroup_Full_Serialization()
        {
            ItemModifierGroup ItemModifierGroup = new ItemModifierGroup();

            List<ItemModifier> _modifiers = new List<ItemModifier>
            {
                new ItemModifier { StringId = "im1" },
                new ItemModifier { StringId = "im2"},
            };

            _itemModifiers.SetValue(ItemModifierGroup, _modifiers);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ItemModifierGroupBinaryPackage package = new ItemModifierGroupBinaryPackage(ItemModifierGroup, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ItemModifierGroupBinaryPackage>(obj);

            ItemModifierGroupBinaryPackage returnedPackage = (ItemModifierGroupBinaryPackage)obj;

            ItemModifierGroup newItemModifierGroup = returnedPackage.Unpack<ItemModifierGroup>();

            Assert.Equal(ItemModifierGroup.NoModifierLootScore, newItemModifierGroup.NoModifierLootScore);
            Assert.Equal(ItemModifierGroup.NoModifierProductionScore, newItemModifierGroup.NoModifierProductionScore);
            Assert.Equal(_modifiers.Count, newItemModifierGroup.ItemModifiers.Count);

            for(int i = 0; i < _modifiers.Count; i++)
            {
                Assert.Equal(ItemModifierGroup.ItemModifiers[i].ToString(), newItemModifierGroup.ItemModifiers[i].ToString());
            }
        }
    }
}
