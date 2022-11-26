using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System.Linq;
using TaleWorlds.Core;
using Xunit;

using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System.Linq;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ItemModifierSerializationTest
    {
        [Fact]
        public void ItemModifier_Serialize()
        {
            ItemModifier itemModifier = new ItemModifier();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ItemModifierBinaryPackage package = new ItemModifierBinaryPackage(itemModifier, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void DynamicBodyProperties_Full_Serialization()
        {
            ItemModifier itemModifier = new ItemModifier();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ItemModifierBinaryPackage package = new ItemModifierBinaryPackage(itemModifier, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ItemModifierBinaryPackage>(obj);

            ItemModifierBinaryPackage returnedPackage = (ItemModifierBinaryPackage)obj;

            ItemModifier newItemModifier = returnedPackage.Unpack<ItemModifier>();

            Assert.True(itemModifier.Equals(newItemModifier));
        }
    }
}
