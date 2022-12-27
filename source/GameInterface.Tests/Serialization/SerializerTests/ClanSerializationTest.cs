using GameInterface.Serialization.Impl;
using GameInterface.Serialization;
using TaleWorlds.Core;
using Xunit;
using System.Linq;
using TaleWorlds.CampaignSystem;
using System.Runtime.Serialization;
using Common.Extensions;
using System.Reflection;
using TaleWorlds.ObjectSystem;
using GameInterface.Tests.Bootstrap;

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

            CampaignTime time = (CampaignTime)FormatterServices.GetUninitializedObject(typeof(CampaignTime));

            testClan.LastFactionChangeTime = time;
            testClan.AutoRecruitmentExpenses = 68;

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
