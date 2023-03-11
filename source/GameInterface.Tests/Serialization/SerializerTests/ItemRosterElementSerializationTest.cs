using GameInterface.Serialization.External;
using GameInterface.Serialization;
using Xunit;
using TaleWorlds.Core;
using System.Reflection;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ItemRosterElementSerializationTest
    {
        [Fact]
        public void ItemRosterElement_Serialize()
        {
            ItemRosterElement itemRosterElement = new ItemRosterElement();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ItemRosterElementBinaryPackage package = new ItemRosterElementBinaryPackage(itemRosterElement, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }
        private static readonly FieldInfo _amount = typeof(ItemRosterElement).GetField("_amount", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly PropertyInfo EquipmentElementProperty = typeof(ItemRosterElement).GetProperty("EquipmentElement");
        [Fact]
        public void ItemRosterElement_Full_Serialization()
        {
            ItemRosterElement itemRosterElement = new ItemRosterElement();
            _amount.SetValue(itemRosterElement, 5);
            EquipmentElementProperty.SetValue(itemRosterElement, new EquipmentElement());
            BinaryPackageFactory factory = new BinaryPackageFactory();
            ItemRosterElementBinaryPackage package = new ItemRosterElementBinaryPackage(itemRosterElement, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ItemRosterElementBinaryPackage>(obj);

            ItemRosterElementBinaryPackage returnedPackage = (ItemRosterElementBinaryPackage)obj;

            ItemRosterElement newRosterElement = returnedPackage.Unpack<ItemRosterElement>();

            //Equals is defined for ItemRosterElement
            Assert.Equal(itemRosterElement, newRosterElement);
        }
    }
}
