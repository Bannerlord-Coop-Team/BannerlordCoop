using Autofac;
using Common.Extensions;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class KingdomSerializationTest
    {
        ILifetimeScope container;
        public KingdomSerializationTest()
        {
            GameBootStrap.Initialize();

            container = SerializationTestContainer.BeginScope();
        }

        [Fact]
        public void Kingdom_Full_Serialization()
        {
            var objectManager = container.Resolve<IObjectManager>();
            Kingdom kingdomObject = (Kingdom)FormatterServices.GetUninitializedObject(typeof(Kingdom));
            // StringId is required as unpack throws exception if null
            kingdomObject.StringId = "My Kingdom";

            objectManager.AddExisting(kingdomObject.StringId, kingdomObject);

            // Assign values
            kingdomObject.LastMercenaryOfferTime = new CampaignTime();
            kingdomObject.NotAttackableByPlayerUntilTime= new CampaignTime();

            // Create settlements for one of the kingdoms cache lists
            Settlement settlement1 = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
            settlement1.StringId = "s1";
            objectManager.AddExisting(settlement1.StringId, settlement1);

            Settlement settlement2 = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
            settlement2.StringId = "s2";
            objectManager.AddExisting(settlement2.StringId, settlement2);

            kingdomObject.InitializeCachedLists();
            List<Settlement> settlements = kingdomObject._settlementsCache;

            settlements.Add(settlement1);
            settlements.Add(settlement2);

            // Create StanceLink for testing
            List<StanceLink> stances = new();

            StanceLink stance = (StanceLink)FormatterServices.GetUninitializedObject(typeof(StanceLink));
            stances.Add(stance);

            // Setup serialization
            var factory = container.Resolve<IBinaryPackageFactory>();
            KingdomBinaryPackage package = new KingdomBinaryPackage(kingdomObject, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<KingdomBinaryPackage>(obj);

            KingdomBinaryPackage returnedPackage = (KingdomBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            Kingdom newKingdomObject = returnedPackage.Unpack<Kingdom>(deserializeFactory);

            Assert.Equal(kingdomObject.LastMercenaryOfferTime, newKingdomObject.LastMercenaryOfferTime);

            List<Settlement> newSettlements = kingdomObject._settlementsCache;
            Assert.Equal(settlements.Count, newSettlements.Count);
            foreach (var zippedSettlements in settlements.Zip(newSettlements, (v1, v2) => (v1, v2)))
            {
                Assert.Equal(zippedSettlements.v1, zippedSettlements.v2);
            }
        }

        [Fact]
        public void Kingdom_WarAndAllyLists_PackedAsIds()
        {
            var objectManager = container.Resolve<IObjectManager>();

            // Overhauls run 40+ kingdoms all at war, by value these lists nested each kingdom inside the last one
            var kingdoms = Enumerable.Range(0, 64).Select(i => CreateKingdom($"war_kingdom_{i}")).ToList();
            foreach (var kingdom in kingdoms)
            {
                objectManager.AddExisting(kingdom.StringId, kingdom);
                kingdom._factionsAtWarWith.AddRange(kingdoms.Where(other => other != kingdom));
            }

            Kingdom testKingdom = CreateKingdom("unregistered_kingdom");
            testKingdom._factionsAtWarWith.AddRange(kingdoms.Skip(1));
            testKingdom._alliedKingdoms.Add(kingdoms[0]);

            var factory = container.Resolve<IBinaryPackageFactory>();
            KingdomBinaryPackage package = new KingdomBinaryPackage(testKingdom, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            KingdomBinaryPackage returnedPackage = BinaryPackageSerializer.Deserialize<KingdomBinaryPackage>(bytes);

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            Kingdom newKingdom = returnedPackage.Unpack<Kingdom>(deserializeFactory);

            Assert.NotSame(testKingdom, newKingdom);
            Assert.Equal(testKingdom._factionsAtWarWith, newKingdom._factionsAtWarWith);
            Assert.Same(kingdoms[0], Assert.Single(newKingdom._alliedKingdoms));
        }

        private static Kingdom CreateKingdom(string stringId)
        {
            Kingdom kingdom = (Kingdom)FormatterServices.GetUninitializedObject(typeof(Kingdom));
            kingdom.StringId = stringId;
            kingdom.InitializeCachedLists();
            return kingdom;
        }

        [Fact]
        public void Kingdom_StringId_Serialization()
        {
            var objectManager = container.Resolve<IObjectManager>();
            Kingdom kingdom = (Kingdom)FormatterServices.GetUninitializedObject(typeof(Kingdom));

            kingdom.StringId = "My Kingdom";
            objectManager.AddExisting(kingdom.StringId, kingdom);

            var factory = container.Resolve<IBinaryPackageFactory>();
            KingdomBinaryPackage package = new KingdomBinaryPackage(kingdom, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<KingdomBinaryPackage>(obj);

            KingdomBinaryPackage returnedPackage = (KingdomBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            Kingdom newKingdom = returnedPackage.Unpack<Kingdom>(deserializeFactory);

            Assert.Same(kingdom, newKingdom);
        }
    }
}
