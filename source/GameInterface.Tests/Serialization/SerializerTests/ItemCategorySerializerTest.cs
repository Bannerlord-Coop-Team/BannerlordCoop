using GameInterface.Serialization.External;
using GameInterface.Serialization;
using TaleWorlds.Core;
using Xunit;
using System.Runtime.Serialization;

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

            Assert.Equal(returnedPackage.stringId, package.stringId);
        }
    }
}
