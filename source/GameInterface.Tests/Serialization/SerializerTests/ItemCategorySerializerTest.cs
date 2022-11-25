using GameInterface.Serialization.Impl;
using GameInterface.Serialization;
using TaleWorlds.Core;
using Xunit;
using System.Runtime.Serialization;
using Common.Extensions;
using System.Reflection;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ItemCategorySerializationTest
    {
        [Fact]
        public void ItemCategory_Serialize()
        {
            ItemCategory testItemCategory = (ItemCategory)FormatterServices.GetUninitializedObject(typeof(ItemCategory));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ItemCategoryBinaryPackage package = new ItemCategoryBinaryPackage(testItemCategory, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void ItemCategory_Full_Serialization()
        {
            ItemCategory testItemCategory = (ItemCategory)FormatterServices.GetUninitializedObject(typeof(ItemCategory));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ItemCategoryBinaryPackage package = new ItemCategoryBinaryPackage(testItemCategory, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ItemCategoryBinaryPackage>(obj);

            ItemCategoryBinaryPackage returnedPackage = (ItemCategoryBinaryPackage)obj;

            ItemCategory newItemCategory = returnedPackage.Unpack<ItemCategory>();

            foreach(FieldInfo field in typeof(ItemCategory).GetAllInstanceFields())
            {
                Assert.Equal(field.GetValue(testItemCategory), field.GetValue(newItemCategory));
            }
        }
    }
}
