using GameInterface.Serialization.Impl;
using GameInterface.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using Xunit;
using TaleWorlds.ObjectSystem;
using System.Reflection;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class EquipmentElementSerializationTest
    {
        [Fact]
        public void EquipmentElement_Serialize()
        {
            EquipmentElement equipmentElement = new EquipmentElement();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            EquipmentElementBinaryPackage package = new EquipmentElementBinaryPackage(equipmentElement, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void EquipmentElement_Full_Serialization()
        {
            MBObjectManager.Init();
            ItemObject itemobj = MBObjectManager.Instance.CreateObject<ItemObject>();
            ItemObject itemobj2 = MBObjectManager.Instance.CreateObject<ItemObject>();
            ItemModifier ItemModifier = MBObjectManager.Instance.CreateObject<ItemModifier>();
            ItemModifier.ModifyDamage(10);
            ItemModifier.ModifyArmor(15);
            EquipmentElement equipmentElement = new EquipmentElement(itemobj,ItemModifier,itemobj2);
            BinaryPackageFactory factory = new BinaryPackageFactory();
            EquipmentElementBinaryPackage package = new EquipmentElementBinaryPackage(equipmentElement, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<EquipmentElementBinaryPackage>(obj);

            EquipmentElementBinaryPackage returnedPackage = (EquipmentElementBinaryPackage)obj;

            EquipmentElement newEquipmentElement = returnedPackage.Unpack<EquipmentElement>();
            
            Assert.Equal(equipmentElement.ItemModifier.GetType().GetField("_damage", BindingFlags.Instance | BindingFlags.NonPublic),
                newEquipmentElement.ItemModifier.GetType().GetField("_damage", BindingFlags.Instance | BindingFlags.NonPublic));

            Assert.Equal(equipmentElement.ItemModifier.GetType().GetField("_armor", BindingFlags.Instance | BindingFlags.NonPublic),
                newEquipmentElement.ItemModifier.GetType().GetField("_armor", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.Equal(equipmentElement.Item.StringId, newEquipmentElement.Item.StringId);
            Assert.Equal(equipmentElement.CosmeticItem.StringId, newEquipmentElement.CosmeticItem.StringId);
            MBObjectManager.Instance.Destroy();
        }
    }
}
