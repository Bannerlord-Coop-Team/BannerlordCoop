using GameInterface.Serialization.External;
using GameInterface.Serialization;
using TaleWorlds.Core;
using Xunit;
using System.Reflection;
using GameInterface.Tests.Bootstrap;
using Autofac;
using Common.Serialization;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Services.ObjectManager;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class EquipmentElementSerializationTest
    {
        IContainer container;
        public EquipmentElementSerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void EquipmentElement_Serialize()
        {
            EquipmentElement equipmentElement = new EquipmentElement();

            var factory = container.Resolve<IBinaryPackageFactory>();
            EquipmentElementBinaryPackage package = new EquipmentElementBinaryPackage(equipmentElement, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void EquipmentElement_Full_Serialization()
        {
            

            ItemObject itemobj = new ItemObject("Item 1");
            ItemObject itemobj2 = new ItemObject("Item 2");
            ItemModifier itemModifier = new ItemModifier()
            {
                StringId = "Modifier 1"
            };
            var objectManager = container.Resolve<IObjectManager>();

            objectManager.AddExisting(itemobj.StringId, itemobj);
            objectManager.AddExisting(itemobj2.StringId, itemobj2);
            objectManager.AddExisting(itemModifier.StringId, itemModifier);

            itemModifier.ModifyDamage(10);
            itemModifier.ModifyArmor(15);

            EquipmentElement equipmentElement = new EquipmentElement(itemobj, itemModifier, itemobj2);
            var factory = container.Resolve<IBinaryPackageFactory>();
            EquipmentElementBinaryPackage package = new EquipmentElementBinaryPackage(equipmentElement, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<EquipmentElementBinaryPackage>(obj);

            EquipmentElementBinaryPackage returnedPackage = (EquipmentElementBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            EquipmentElement newEquipmentElement = returnedPackage.Unpack<EquipmentElement>(deserializeFactory);
            
            Assert.Equal(equipmentElement.ItemModifier.Damage,
                         newEquipmentElement.ItemModifier.Damage);

            Assert.Equal(equipmentElement.ItemModifier.Armor,
                         newEquipmentElement.ItemModifier.Armor);

            Assert.Equal(equipmentElement.Item.StringId, newEquipmentElement.Item.StringId);
            Assert.Equal(equipmentElement.CosmeticItem.StringId, newEquipmentElement.CosmeticItem.StringId);
        }
    }
}
