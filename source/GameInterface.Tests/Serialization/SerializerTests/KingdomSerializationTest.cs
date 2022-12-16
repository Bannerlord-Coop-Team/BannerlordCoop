using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Xunit;
using Xunit.Abstractions;

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
            Kingdom kingdomObject = (Kingdom)FormatterServices.GetUninitializedObject(typeof(Kingdom));           

            BinaryPackageFactory factory = new BinaryPackageFactory();
            KingdomBinaryPackage package = new KingdomBinaryPackage(kingdomObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Kingdom_Full_Serialization()
        {
            Kingdom kingdomObject = (Kingdom)FormatterServices.GetUninitializedObject(typeof(Kingdom));

            kingdomObject.LastMercenaryOfferTime = new CampaignTime();
            kingdomObject.StringId = "Kingdom";
            kingdomObject.NotAttackableByPlayerUntilTime= new CampaignTime();
            kingdomObject.GetType().GetProperty("LastArmyCreationDay", BindingFlags.Instance | BindingFlags.Public).SetValue(kingdomObject, 5);

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
            Assert.Equal(kingdomObject.StringId, newKingdomObject.StringId);
            Assert.Equal(kingdomObject.LastArmyCreationDay, newKingdomObject.LastArmyCreationDay);
        }
    }
}
