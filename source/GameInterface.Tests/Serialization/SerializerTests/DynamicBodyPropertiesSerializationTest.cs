using GameInterface.Serialization;
using GameInterface.Serialization.External;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class DynamicBodyPropertiesBinaryPackageSerializationTest
    {
        [Fact]
        public void DynamicBodyProperties_Serialize()
        {
            DynamicBodyProperties DynamicBodyProperties = new DynamicBodyProperties();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            DynamicBodyPropertiesBinaryPackage package = new DynamicBodyPropertiesBinaryPackage(DynamicBodyProperties, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void DynamicBodyProperties_Full_Serialization()
        {
            DynamicBodyProperties DynamicBodyProperties = new DynamicBodyProperties(37, 17, 43);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            DynamicBodyPropertiesBinaryPackage package = new DynamicBodyPropertiesBinaryPackage(DynamicBodyProperties, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<DynamicBodyPropertiesBinaryPackage>(obj);

            DynamicBodyPropertiesBinaryPackage returnedPackage = (DynamicBodyPropertiesBinaryPackage)obj;

            DynamicBodyProperties newStaticBodyProperties = returnedPackage.Unpack<DynamicBodyProperties>();

            Assert.Equal(DynamicBodyProperties.Age, newStaticBodyProperties.Age);
            Assert.Equal(DynamicBodyProperties.Weight, newStaticBodyProperties.Weight);
            Assert.Equal(DynamicBodyProperties.Build, newStaticBodyProperties.Build);
        }
    }
}
