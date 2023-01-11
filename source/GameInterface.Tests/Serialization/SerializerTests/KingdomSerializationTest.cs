using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class KingdomSerializationTest
    {
        public KingdomSerializationTest()
        {
            GameBootStrap.Initialize();
        }

        [Fact]
        public void Kingdom_Serialize()
        {
            Kingdom kingdomObject = new Kingdom();           

            BinaryPackageFactory factory = new BinaryPackageFactory();
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
            Kingdom kingdomObject = new Kingdom();
            // StringId is required as unpack throws exception if null
            kingdomObject.StringId = "My Kingdom";

            // Assign values
            kingdomObject.LastMercenaryOfferTime = new CampaignTime();
            kingdomObject.NotAttackableByPlayerUntilTime= new CampaignTime();
            Kingdom_LastArmyCreationDay.SetRandom(kingdomObject);

            // Create settlements for one of the kingdoms cache lists
            Settlement settlement1 = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
            settlement1.StringId = "s1";
            MBObjectManager.Instance.RegisterObject(settlement1);

            Settlement settlement2 = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
            settlement2.StringId = "s2";
            MBObjectManager.Instance.RegisterObject(settlement2);

            KingdomBinaryPackage.InitializeCachedLists.Invoke(kingdomObject, new object[0]);
            List<Settlement> settlements = (List<Settlement>)KingdomBinaryPackage.Kingdom_Settlements.GetValue(kingdomObject);

            settlements.Add(settlement1);
            settlements.Add(settlement2);

            // Create StanceLink for testing
            List<StanceLink> stances = (List<StanceLink>)Kingdom_Stances.GetValue(kingdomObject);

            StanceLink stance = (StanceLink)FormatterServices.GetUninitializedObject(typeof(StanceLink));
            stances.Add(stance);

            // Setup serialization
            BinaryPackageFactory factory = new BinaryPackageFactory();
            KingdomBinaryPackage package = new KingdomBinaryPackage(kingdomObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<KingdomBinaryPackage>(obj);

            KingdomBinaryPackage returnedPackage = (KingdomBinaryPackage)obj;

            Kingdom newKingdomObject = returnedPackage.Unpack<Kingdom>();

            Assert.Equal(kingdomObject.LastMercenaryOfferTime, newKingdomObject.LastMercenaryOfferTime);
            Assert.Equal(kingdomObject.LastArmyCreationDay, newKingdomObject.LastArmyCreationDay);

            List<Settlement> newSettlements = (List<Settlement>)KingdomBinaryPackage.Kingdom_Settlements.GetValue(newKingdomObject);
            Assert.Equal(settlements.Count, newSettlements.Count);
            foreach (var zippedSettlements in settlements.Zip(newSettlements, (v1, v2) => (v1, v2)))
            {
                Assert.Equal(zippedSettlements.v1, zippedSettlements.v2);
            }
        }

        [Fact]
        public void Kingdom_StringId_Serialization()
        {
            Kingdom kingdom = (Kingdom)FormatterServices.GetUninitializedObject(typeof(Kingdom));

            kingdom.StringId = "My Kingdom";
            MBObjectManager.Instance.RegisterObject(kingdom);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            KingdomBinaryPackage package = new KingdomBinaryPackage(kingdom, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<KingdomBinaryPackage>(obj);

            KingdomBinaryPackage returnedPackage = (KingdomBinaryPackage)obj;

            Kingdom newKingdom = returnedPackage.Unpack<Kingdom>();

            Assert.Same(kingdom, newKingdom);
        }
    }
}
