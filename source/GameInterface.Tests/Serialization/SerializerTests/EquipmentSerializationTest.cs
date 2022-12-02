using GameInterface.Serialization.Impl;
using GameInterface.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using Xunit;
using System.Reflection;
using static TaleWorlds.Core.Equipment;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class EquipmentSerializationTest
    {
        [Fact]
        public void Equipment_Serialize()
        {
            Equipment equipment = new Equipment();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            EquipmentBinaryPackage package = new EquipmentBinaryPackage(equipment, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Equipment_Full_Serialization()
        {
            Equipment equipment = new Equipment();
            FieldInfo _equipmentType = typeof(Equipment).GetField("_equipmentType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            _equipmentType.SetValue(equipment,EquipmentType.Battle);
            //TODO: populate the 12 item slots.
            BinaryPackageFactory factory = new BinaryPackageFactory();
            EquipmentBinaryPackage package = new EquipmentBinaryPackage(equipment, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<EquipmentBinaryPackage>(obj);

            EquipmentBinaryPackage returnedPackage = (EquipmentBinaryPackage)obj;

            Equipment newEquipment = returnedPackage.Unpack<Equipment>();

            Assert.True(equipment.IsCivilian == newEquipment.IsCivilian && equipment.IsValid == newEquipment.IsValid && _equipmentType.GetValue(equipment) == _equipmentType.GetValue(newEquipment));
            //TODO: Assert the 12 item slots.
        }
    }
}
