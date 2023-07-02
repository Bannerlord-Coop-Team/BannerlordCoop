﻿using GameInterface.Serialization.External;
using GameInterface.Serialization;
using TaleWorlds.Core;
using Xunit;
using System.Reflection;
using static TaleWorlds.Core.Equipment;
using Autofac;
using Common.Serialization;
using GameInterface.Tests.Bootstrap.Modules;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class EquipmentSerializationTest
    {
        IContainer container;
        public EquipmentSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void Equipment_Serialize()
        {
            Equipment equipment = new Equipment();

            var factory = container.Resolve<IBinaryPackageFactory>();
            EquipmentBinaryPackage package = new EquipmentBinaryPackage(equipment, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly FieldInfo _equipmentType = typeof(Equipment).GetField("_equipmentType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        [Fact]
        public void Equipment_Full_Serialization()
        {
            Equipment equipment = new Equipment();
            _equipmentType.SetValue(equipment, EquipmentType.Battle);

            for (int i = 0; i < 12; i++)
            {
                equipment[i] = new EquipmentElement();
            }
            var factory = container.Resolve<IBinaryPackageFactory>();
            EquipmentBinaryPackage package = new EquipmentBinaryPackage(equipment, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<EquipmentBinaryPackage>(obj);

            EquipmentBinaryPackage returnedPackage = (EquipmentBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            Equipment newEquipment = returnedPackage.Unpack<Equipment>(deserializeFactory);

            Assert.Equal(equipment.IsCivilian, newEquipment.IsCivilian);
            Assert.Equal(equipment.IsValid, newEquipment.IsValid);
            Assert.Equal((EquipmentType)_equipmentType.GetValue(equipment), (EquipmentType)_equipmentType.GetValue(newEquipment));
            for (int i = 0; i < 12; i++)
            {
                Assert.True(equipment[i].IsEqualTo(newEquipment[i]));
            }
        }
    }
}
