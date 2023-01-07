using GameInterface.Serialization;
using GameInterface.Serialization.External;
using System.Reflection;
using TaleWorlds.Core;
using Xunit;

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

        static FieldInfo _damage = typeof(ItemModifier).GetField("_damage", BindingFlags.Instance | BindingFlags.NonPublic);
        static FieldInfo _armor = typeof(ItemModifier).GetField("_armor", BindingFlags.Instance | BindingFlags.NonPublic);
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

            Assert.Equal(_damage.GetValue(ItemModifier),
                         _damage.GetValue(newItemModifier));

            Assert.Equal(_armor.GetValue(ItemModifier),
                         _armor.GetValue(newItemModifier));
        }
    }
}
