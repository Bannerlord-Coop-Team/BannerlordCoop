using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class StanceLinkSerializationTest
    {
        IContainer container;
        public StanceLinkSerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void StanceLink_Serialize()
        {
            StanceLink stanceLink = (StanceLink)FormatterServices.GetUninitializedObject(typeof(StanceLink));

            var factory = container.Resolve<IBinaryPackageFactory>();
            StanceLinkBinaryPackage package = new StanceLinkBinaryPackage(stanceLink, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void StanceLink_Full_Serialization()
        {
            var objectManager = container.Resolve<IObjectManager>();
            StanceLink stanceLink = (StanceLink)FormatterServices.GetUninitializedObject(typeof(StanceLink));

            // Setup stanceLink factions
            Clan clan1 = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
            Clan clan2 = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));

            clan1.StringId = "clan1";
            clan2.StringId = "clan2";

            objectManager.AddExisting(clan1.StringId, clan1);
            objectManager.AddExisting(clan2.StringId, clan2);

            stanceLink.Faction1 = clan1;
            stanceLink.Faction2 = clan2;

            // Serialize stanceLink
            var factory = container.Resolve<IBinaryPackageFactory>();
            StanceLinkBinaryPackage package = new StanceLinkBinaryPackage(stanceLink, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<StanceLinkBinaryPackage>(obj);

            StanceLinkBinaryPackage returnedPackage = (StanceLinkBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            StanceLink newStanceLink = returnedPackage.Unpack<StanceLink>(deserializeFactory);

            Assert.Equal(stanceLink.Faction1, newStanceLink.Faction1);
            Assert.Equal(stanceLink.Faction2, newStanceLink.Faction2);
            Assert.Equal(stanceLink.Casualties1, newStanceLink.Casualties1);
        }
    }
}
