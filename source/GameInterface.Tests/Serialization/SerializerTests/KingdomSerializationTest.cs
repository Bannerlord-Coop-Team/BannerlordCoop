using Autofac;
using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class KingdomSerializationTest
    {
        IContainer container;
        public KingdomSerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void Kingdom_Serialize()
        {
            Kingdom kingdomObject = (Kingdom)FormatterServices.GetUninitializedObject(typeof(Kingdom));

            var factory = container.Resolve<IBinaryPackageFactory>();
            KingdomBinaryPackage package = new KingdomBinaryPackage(kingdomObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly PropertyInfo Kingdom_LastArmyCreationDay = typeof(Kingdom).GetProperty("LastArmyCreationDay", BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo Kingdom_Stances = typeof(Kingdom).GetField("_stances", BindingFlags.NonPublic | BindingFlags.Instance);
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
            Kingdom_LastArmyCreationDay.SetRandom(kingdomObject);

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
            List<StanceLink> stances = (List<StanceLink>)Kingdom_Stances.GetValue(kingdomObject);

            StanceLink stance = (StanceLink)FormatterServices.GetUninitializedObject(typeof(StanceLink));
            stances.Add(stance);

            // Setup serialization
            var factory = container.Resolve<IBinaryPackageFactory>();
            KingdomBinaryPackage package = new KingdomBinaryPackage(kingdomObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<KingdomBinaryPackage>(obj);

            KingdomBinaryPackage returnedPackage = (KingdomBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            Kingdom newKingdomObject = returnedPackage.Unpack<Kingdom>(deserializeFactory);

            Assert.Equal(kingdomObject.LastMercenaryOfferTime, newKingdomObject.LastMercenaryOfferTime);
            Assert.Equal(kingdomObject.LastArmyCreationDay, newKingdomObject.LastArmyCreationDay);

            List<Settlement> newSettlements = kingdomObject._settlementsCache;
            Assert.Equal(settlements.Count, newSettlements.Count);
            foreach (var zippedSettlements in settlements.Zip(newSettlements, (v1, v2) => (v1, v2)))
            {
                Assert.Equal(zippedSettlements.v1, zippedSettlements.v2);
            }
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

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<KingdomBinaryPackage>(obj);

            KingdomBinaryPackage returnedPackage = (KingdomBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            Kingdom newKingdom = returnedPackage.Unpack<Kingdom>(deserializeFactory);

            Assert.Same(kingdom, newKingdom);
        }
    }
}
