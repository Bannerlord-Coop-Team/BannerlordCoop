using Xunit;
using System.Runtime.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Serialization;
using TaleWorlds.CampaignSystem.Party;
using Autofac;
using Common.Serialization;
using GameInterface.Tests.Bootstrap.Modules;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class PartyBaseSerializationTest
    {
        IContainer container;
        public PartyBaseSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void PartyBase_Serialize()
        {
            PartyBase testPartyObject = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));

            var factory = container.Resolve<IBinaryPackageFactory>();
            PartyBaseBinaryPackage package = new PartyBaseBinaryPackage(testPartyObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void PartyBase_Full_Serialization()
        {
            PartyBase testPartyObject = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));

            testPartyObject.RemainingFoodPercentage = 5;
            testPartyObject.GetType().GetProperty("Index", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).SetValue(testPartyObject, 5);

            var factory = container.Resolve<IBinaryPackageFactory>();
            PartyBaseBinaryPackage package = new PartyBaseBinaryPackage(testPartyObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<PartyBaseBinaryPackage>(obj);

            PartyBaseBinaryPackage returnedPackage = (PartyBaseBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            PartyBase returnedPartyObject = returnedPackage.Unpack<PartyBase>(deserializeFactory);

            Assert.Equal(testPartyObject.RemainingFoodPercentage, returnedPartyObject.RemainingFoodPercentage);
            Assert.Equal(testPartyObject.Index, returnedPartyObject.Index);
        }
    }
}