using GameInterface.Serialization.External;
using GameInterface.Serialization;
using Xunit;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class VillageSerializationTest
    {
        [Fact]
        public void Village_Serialize()
        {
            Village testVillage = (Village)FormatterServices.GetUninitializedObject(typeof(Village));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            VillageBinaryPackage package = new VillageBinaryPackage(testVillage, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Village_Full_Serialization()
        {
            Village testVillage = new Village();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            VillageBinaryPackage package = new VillageBinaryPackage(testVillage, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<VillageBinaryPackage>(obj);

            VillageBinaryPackage returnedPackage = (VillageBinaryPackage)obj;

            Assert.Equal(returnedPackage.StringId, package.StringId);
        }
    }
}
