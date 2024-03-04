﻿using GameInterface.Serialization.External;
using GameInterface.Serialization;
using Xunit;
using TaleWorlds.CampaignSystem;
using System.Runtime.Serialization;
using TaleWorlds.ObjectSystem;
using GameInterface.Tests.Bootstrap;
using System.Collections.Generic;
using TaleWorlds.Library;
using Autofac;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Services.ObjectManager;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ClanSerializationTest
    {
        IContainer container;
        public ClanSerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void Clan_Serialize()
        {
            Clan testClan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));

            var factory = container.Resolve<IBinaryPackageFactory>();
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
            var objectManager = container.Resolve<IObjectManager>();

            hero1.StringId = "myHero1";
            hero2.StringId = "myHero2";

            objectManager.AddExisting(hero1.StringId, hero1);
            objectManager.AddExisting(hero2.StringId, hero2);

            CampaignTime time = (CampaignTime)FormatterServices.GetUninitializedObject(typeof(CampaignTime));

            testClan.LastFactionChangeTime = time;
            testClan.AutoRecruitmentExpenses = 68;

            var heroes = new MBList<Hero>
            {
                hero1,
                hero2
            };

            testClan._supporterNotablesCache = heroes;
            testClan._companionsCache = heroes;
            testClan._lordsCache = heroes;
            testClan._supporterNotablesCache = heroes; // Again?

            var factory = container.Resolve<IBinaryPackageFactory>();
            ClanBinaryPackage package = new ClanBinaryPackage(testClan, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ClanBinaryPackage>(obj);

            ClanBinaryPackage returnedPackage = (ClanBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            Clan newClan = returnedPackage.Unpack<Clan>(deserializeFactory);

            Assert.Equal(testClan.AutoRecruitmentExpenses, newClan.AutoRecruitmentExpenses);
            Assert.Equal(testClan.LastFactionChangeTime, newClan.LastFactionChangeTime);

            Assert.Equal(heroes, newClan._supporterNotablesCache);
            Assert.Equal(heroes, newClan._companionsCache);
            Assert.Equal(heroes, newClan._lordsCache);
            Assert.Equal(heroes, newClan._supporterNotablesCache);
        }

        [Fact]
        public void Clan_StringId_Serialization()
        {
            Clan clan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
            var objectManager = container.Resolve<IObjectManager>();

            clan.StringId = "My Clan";
            objectManager.AddExisting(clan.StringId, clan);

            var factory = container.Resolve<IBinaryPackageFactory>();
            ClanBinaryPackage package = new ClanBinaryPackage(clan, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ClanBinaryPackage>(obj);

            ClanBinaryPackage returnedPackage = (ClanBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            Clan newHero = returnedPackage.Unpack<Clan>(deserializeFactory);

            Assert.Same(clan, newHero);
        }
    }
}
