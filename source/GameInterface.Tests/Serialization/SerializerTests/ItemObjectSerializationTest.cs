using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Xunit;
using static TaleWorlds.Core.WeaponComponentData;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ItemObjectSerializationTest
    {
        public ItemObjectSerializationTest()
        {
            GameBootStrap.Initialize();
        }

        [Fact]
        public void ItemObject_Serialize()
        {
            ItemObject itemObject = (ItemObject)FormatterServices.GetUninitializedObject(typeof(ItemObject));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ItemObjectBinaryPackage package = new ItemObjectBinaryPackage(itemObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void ItemObject_Full_Serialization()
        {
            ItemObject itemObject = new ItemObject("myItem");

            WeaponComponentData weaponComponentData = new WeaponComponentData(itemObject);
            weaponComponentData.Init("testName", "Cu", "slicy", new DamageTypes(), new DamageTypes(), 9, 57, 45, 34, 12, 54, 23, 78, 34, "testname", 11, 56, new MatrixFrame(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12), new WeaponClass(), 70, 71, 72, 73, 75, new Vec3(1, 2, 3), new WeaponTiers(), 4);

            itemObject.AddWeapon(weaponComponentData, new ItemModifierGroup());

            BinaryPackageFactory factory = new BinaryPackageFactory();

            byte[] bytes = BinaryFormatterSerializer.Serialize(factory.GetBinaryPackage(itemObject));

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ItemObjectBinaryPackage>(obj);

            ItemObjectBinaryPackage returnedPackage = (ItemObjectBinaryPackage)obj;

            ItemObject newItemObject = returnedPackage.Unpack<ItemObject>();

            Assert.Equal(itemObject.Name, newItemObject.Name);
            Assert.Equal(itemObject.MultiMeshName, newItemObject.MultiMeshName);
            Assert.Equal(itemObject.Value, newItemObject.Value);
            Assert.Equal(itemObject.BodyName, newItemObject.BodyName);
        }

        [Fact]
        public void ItemObject_StringId_Serialization()
        {
            ItemObject itemObject = MBObjectManager.Instance.CreateObject<ItemObject>();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ItemObjectBinaryPackage package = new ItemObjectBinaryPackage(itemObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ItemObjectBinaryPackage>(obj);

            ItemObjectBinaryPackage returnedPackage = (ItemObjectBinaryPackage)obj;

            ItemObject newItemObject = returnedPackage.Unpack<ItemObject>();

            Assert.Same(itemObject, newItemObject);
        }
    }
}
