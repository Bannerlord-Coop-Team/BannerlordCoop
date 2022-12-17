using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using GameInterface.Tests.Bootstrap;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class WorkshopSerializationTest
    {
        public WorkshopSerializationTest()
        {
            GameBootStrap.Initialize();
        }

        [Fact]
        public void Workshop_Serialize()
        {
            Workshop Workshop = (Workshop)FormatterServices.GetUninitializedObject(typeof(Workshop));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            WorkshopBinaryPackage package = new WorkshopBinaryPackage(Workshop, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly PropertyInfo Capital = typeof(Workshop).GetProperty(nameof(Workshop.Capital));
        private static readonly PropertyInfo ConstructionTimeRemained = typeof(Workshop).GetProperty(nameof(Workshop.ConstructionTimeRemained));
        private static readonly PropertyInfo InitialCapital = typeof(Workshop).GetProperty(nameof(Workshop.InitialCapital));
        private static readonly PropertyInfo InsideParty = typeof(Workshop).GetProperty(nameof(Workshop.InsideParty));
        private static readonly PropertyInfo Level = typeof(Workshop).GetProperty(nameof(Workshop.Level));
        private static readonly PropertyInfo NotRunnedDays = typeof(Workshop).GetProperty(nameof(Workshop.NotRunnedDays));
        private static readonly PropertyInfo Upgradable = typeof(Workshop).GetProperty(nameof(Workshop.Upgradable));
        private static readonly FieldInfo _customName = typeof(Workshop).GetField("_customName", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _owner = typeof(Workshop).GetField("_owner", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _productionProgress = typeof(Workshop).GetField("_productionProgress", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _settlement = typeof(Workshop).GetField("_settlement", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _tag = typeof(Workshop).GetField("_tag", BindingFlags.NonPublic | BindingFlags.Instance);
        [Fact]
        public void Workshop_Full_Serialization()
        {
            Settlement settlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
            MobileParty party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
            Hero hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));

            settlement.StringId = "mySettlement";
            party.StringId = "myParty";
            hero.StringId = "myHero";

            MBObjectManager.Instance.RegisterObject(settlement);
            MBObjectManager.Instance.RegisterObject(party);
            MBObjectManager.Instance.RegisterObject(hero);

            Workshop Workshop = new Workshop(settlement, "ws");

            Capital.SetValue(Workshop, 5);
            ConstructionTimeRemained.SetValue(Workshop, 100);
            InitialCapital.SetValue(Workshop, 3);
            InsideParty.SetValue(Workshop, party);
            Level.SetValue(Workshop, 2);
            NotRunnedDays.SetValue(Workshop, 566);
            Upgradable.SetValue(Workshop, true);
            _customName.SetValue(Workshop, new TextObject("I have a custom name"));
            _owner.SetValue(Workshop, hero);
            _productionProgress.SetValue(Workshop, new float[] { 0.6f, 0.7f });

            BinaryPackageFactory factory = new BinaryPackageFactory();
            WorkshopBinaryPackage package = new WorkshopBinaryPackage(Workshop, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<WorkshopBinaryPackage>(obj);

            WorkshopBinaryPackage returnedPackage = (WorkshopBinaryPackage)obj;

            Workshop newWorkshop = returnedPackage.Unpack<Workshop>();

            Assert.Equal(Workshop.Capital, newWorkshop.Capital);
            Assert.Equal(Workshop.ConstructionTimeRemained, newWorkshop.ConstructionTimeRemained);
            Assert.Equal(Workshop.InitialCapital, newWorkshop.InitialCapital);
            Assert.Equal(Workshop.Level, newWorkshop.Level);
            Assert.Equal(Workshop.NotRunnedDays, newWorkshop.NotRunnedDays);
            Assert.Equal(Workshop.Upgradable, newWorkshop.Upgradable);
            Assert.Equal(_productionProgress.GetValue(Workshop), _productionProgress.GetValue(newWorkshop));
            Assert.Equal(_settlement.GetValue(Workshop), _settlement.GetValue(newWorkshop));
            Assert.Equal(_tag.GetValue(Workshop), _tag.GetValue(newWorkshop));

            Assert.Equal(_customName.GetValue(Workshop).ToString(), _customName.GetValue(newWorkshop).ToString());

            Assert.Equal(Workshop.InsideParty, newWorkshop.InsideParty);
            Assert.Equal(_owner.GetValue(Workshop), _owner.GetValue(newWorkshop));
        }
    }
}
