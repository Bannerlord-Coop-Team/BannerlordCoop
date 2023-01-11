using GameInterface.Serialization.External;
using GameInterface.Serialization;
using TaleWorlds.Core;
using Xunit;
using System.Runtime.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class BasicCultureObjectSerializationTest
    {
        [Fact]
        public void BasicCultureObject_Serialize()
        {
            BasicCultureObject testBasicCultureObject = (BasicCultureObject)FormatterServices.GetUninitializedObject(typeof(BasicCultureObject));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            BasicCultureObjectBinaryPackage package = new BasicCultureObjectBinaryPackage(testBasicCultureObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void BasicCultureObject_Full_Serialization()
        {
            BasicCultureObject testBasicCultureObject = (BasicCultureObject)FormatterServices.GetUninitializedObject(typeof(BasicCultureObject));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            BasicCultureObjectBinaryPackage package = new BasicCultureObjectBinaryPackage(testBasicCultureObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<BasicCultureObjectBinaryPackage>(obj);

            BasicCultureObjectBinaryPackage returnedPackage = (BasicCultureObjectBinaryPackage)obj;

            Assert.Equal(returnedPackage.stringId, package.stringId);
        }
    }
}
