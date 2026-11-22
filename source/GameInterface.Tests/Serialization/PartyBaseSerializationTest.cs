using Autofac;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class PartyBaseSerializationTest
    {
        ILifetimeScope container;
        public PartyBaseSerializationTest()
        {
            container = SerializationTestContainer.BeginScope();
        }

        [Fact]
        public void PartyBase_Full_Serialization()
        {
            PartyBase testPartyObject = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));

            testPartyObject.RemainingFoodPercentage = 5;
            testPartyObject.Index = 5;

            var factory = container.Resolve<IBinaryPackageFactory>();
            PartyBaseBinaryPackage package = new PartyBaseBinaryPackage(testPartyObject, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<PartyBaseBinaryPackage>(obj);

            PartyBaseBinaryPackage returnedPackage = (PartyBaseBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            PartyBase returnedPartyObject = returnedPackage.Unpack<PartyBase>(deserializeFactory);

            Assert.Equal(testPartyObject.RemainingFoodPercentage, returnedPartyObject.RemainingFoodPercentage);
            Assert.Equal(testPartyObject.Index, returnedPartyObject.Index);
        }
    }
}