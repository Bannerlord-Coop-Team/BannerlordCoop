using Autofac;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CultureObjectSerializationTest
    {
        ILifetimeScope container;
        public CultureObjectSerializationTest()
        {
            container = SerializationTestContainer.BeginScope();
        }

        [Fact]
        public void CultureObject_Full_Serialization()
        {
            CultureObject testCultureObject = (CultureObject)FormatterServices.GetUninitializedObject(typeof(CultureObject));

            var factory = container.Resolve<IBinaryPackageFactory>();
            CultureObjectBinaryPackage package = new CultureObjectBinaryPackage(testCultureObject, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<CultureObjectBinaryPackage>(obj);

            CultureObjectBinaryPackage returnedPackage = (CultureObjectBinaryPackage)obj;

            Assert.Equal(returnedPackage.StringId, package.StringId);
        }
    }
}
