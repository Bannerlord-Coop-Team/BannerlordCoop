using GameInterface.Serialization.External;
using GameInterface.Serialization;
using Xunit;
using TaleWorlds.CampaignSystem;
using System.Runtime.Serialization;
using TaleWorlds.ObjectSystem;
using GameInterface.Tests.Bootstrap;
using System.Collections.Generic;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ClanSerializationTest
    {
        public ClanSerializationTest()
        {
            GameBootStrap.Initialize();
        }

        [Fact]
        public void Clan_Serialize()
        {
            Clan testClan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ClanBinaryPackage package = new ClanBinaryPackage(testClan, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Clan_Full_Serialization()
        {
            Clan testClan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
            Hero hero1 = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            Hero hero2 = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));

            hero1.StringId = "myHero1";
            hero2.StringId = "myHero2";

            MBObjectManager.Instance.RegisterObject(hero1);
            MBObjectManager.Instance.RegisterObject(hero2);

            CampaignTime time = (CampaignTime)FormatterServices.GetUninitializedObject(typeof(CampaignTime));

            testClan.LastFactionChangeTime = time;
            testClan.AutoRecruitmentExpenses = 68;

            List<Hero> heroes = new List<Hero>
            {
                hero1,
                hero2
            };

            ClanBinaryPackage.Clan_supporterNotablesCache.SetValue(testClan, heroes);
            ClanBinaryPackage.Clan_companionsCache.SetValue(testClan, heroes);
            ClanBinaryPackage.Clan_lordsCache.SetValue(testClan, heroes);
            ClanBinaryPackage.Clan_supporterNotablesCache.SetValue(testClan, heroes);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ClanBinaryPackage package = new ClanBinaryPackage(testClan, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ClanBinaryPackage>(obj);

            ClanBinaryPackage returnedPackage = (ClanBinaryPackage)obj;

            Clan newClan = returnedPackage.Unpack<Clan>();

            Assert.Equal(testClan.AutoRecruitmentExpenses, newClan.AutoRecruitmentExpenses);
            Assert.Equal(testClan.LastFactionChangeTime, newClan.LastFactionChangeTime);

            Assert.Equal(heroes, ClanBinaryPackage.Clan_supporterNotablesCache.GetValue(newClan));
            Assert.Equal(heroes, ClanBinaryPackage.Clan_companionsCache.GetValue(newClan));
            Assert.Equal(heroes, ClanBinaryPackage.Clan_lordsCache.GetValue(newClan));
            Assert.Equal(heroes, ClanBinaryPackage.Clan_supporterNotablesCache.GetValue(newClan));
        }

        [Fact]
        public void Clan_StringId_Serialization()
        {
            Clan clan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
            clan.StringId = "My Clan";
            MBObjectManager.Instance.RegisterObject(clan);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ClanBinaryPackage package = new ClanBinaryPackage(clan, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ClanBinaryPackage>(obj);

            ClanBinaryPackage returnedPackage = (ClanBinaryPackage)obj;

            Clan newHero = returnedPackage.Unpack<Clan>();

            Assert.Same(clan, newHero);
        }
    }
}
