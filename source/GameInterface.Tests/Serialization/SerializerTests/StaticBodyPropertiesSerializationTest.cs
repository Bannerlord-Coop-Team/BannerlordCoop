using GameInterface.Serialization;
using GameInterface.Serialization.External;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class StaticBodyPropertiesSerializationTest
    {
        [Fact]
        public void StaticBodyProperties_Serialize()
        {
            StaticBodyProperties staticBodyProperties = new StaticBodyProperties();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            StaticBodyPropertiesBinaryPackage package = new StaticBodyPropertiesBinaryPackage(staticBodyProperties, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void StaticBodyProperties_Full_Serialization()
        {
            StaticBodyProperties staticBodyProperties = new StaticBodyProperties(1, 2, 3, 4, 5, 6, 7, 8);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            StaticBodyPropertiesBinaryPackage package = new StaticBodyPropertiesBinaryPackage(staticBodyProperties, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<StaticBodyPropertiesBinaryPackage>(obj);

            StaticBodyPropertiesBinaryPackage returnedPackage = (StaticBodyPropertiesBinaryPackage)obj;

            StaticBodyProperties newStaticBodyProperties = returnedPackage.Unpack<StaticBodyProperties>();

            Assert.Equal(staticBodyProperties.KeyPart1, newStaticBodyProperties.KeyPart1);
            Assert.Equal(staticBodyProperties.KeyPart2, newStaticBodyProperties.KeyPart2);
            Assert.Equal(staticBodyProperties.KeyPart3, newStaticBodyProperties.KeyPart3);
            Assert.Equal(staticBodyProperties.KeyPart4, newStaticBodyProperties.KeyPart4);
            Assert.Equal(staticBodyProperties.KeyPart5, newStaticBodyProperties.KeyPart5);
            Assert.Equal(staticBodyProperties.KeyPart6, newStaticBodyProperties.KeyPart6);
            Assert.Equal(staticBodyProperties.KeyPart7, newStaticBodyProperties.KeyPart7);
            Assert.Equal(staticBodyProperties.KeyPart8, newStaticBodyProperties.KeyPart8);
        }
    }
}
