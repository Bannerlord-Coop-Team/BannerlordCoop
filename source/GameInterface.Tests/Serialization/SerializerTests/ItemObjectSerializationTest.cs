using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ItemObjectSerializationTest
    {
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
            ItemObject itemObject = (ItemObject)FormatterServices.GetUninitializedObject(typeof(ItemObject));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ItemObjectBinaryPackage package = new ItemObjectBinaryPackage(itemObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ItemObjectBinaryPackage>(obj);

            ItemObjectBinaryPackage returnedPackage = (ItemObjectBinaryPackage)obj;

            Assert.Equal(package.stringId, returnedPackage.stringId);

        }
    }
}
