using GameInterface.Serialization.External;
using GameInterface.Serialization;
using Xunit;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CultureObjectSerializationTest
    {
        [Fact]
        public void CultureObject_Serialize()
        {
            CultureObject testCultureObject = (CultureObject)FormatterServices.GetUninitializedObject(typeof(CultureObject));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CultureObjectBinaryPackage package = new CultureObjectBinaryPackage(testCultureObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void CultureObject_Full_Serialization()
        {
            CultureObject testCultureObject = (CultureObject)FormatterServices.GetUninitializedObject(typeof(CultureObject));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CultureObjectBinaryPackage package = new CultureObjectBinaryPackage(testCultureObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CultureObjectBinaryPackage>(obj);

            CultureObjectBinaryPackage returnedPackage = (CultureObjectBinaryPackage)obj;

            Assert.Equal(returnedPackage.StringId, package.StringId);
        }
    }
}
