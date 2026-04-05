using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using System.Runtime.InteropServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;
using TaleWorlds.Core;
using Autofac.Features.OwnedInstances;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem.Issues;
using static TaleWorlds.CampaignSystem.Issues.BettingFraudIssueBehavior;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace E2E.Tests.Services.Heroes
{
    public class HeroSyncTests : SyncTestBase
    {
        private string HeroId;
        private string OtherHeroId;
        private string ClanId;
        private string SettlementId;
        private string TownId;
        private string MobilePartyId;
        private string CivEquipmentId;
        private string BattleEquipmentId;

        StaticBodyProperties body = new StaticBodyProperties(1, 2, 1, 2, 1, 3, 1, 2);
        float newFloat = 5f;
        int newInt = 9;
        long newLong = 99;
        TextObject newText = new TextObject("testText");
        CampaignTime newCampaignTime = new CampaignTime(999);
        FormationClass newFormation = new FormationClass();
        Hero.CharacterStates newCharState = Hero.CharacterStates.Released;
        Occupation newOccupation = Occupation.Mercenary;
        KillCharacterAction.KillCharacterActionDetail newKillAction = KillCharacterAction.KillCharacterActionDetail.Murdered;
        EquipmentElement newEquipmentElement = new EquipmentElement();

        public HeroSyncTests(ITestOutputHelper output) : base(output)
        {
            HeroId = TestEnvironment.CreateRegisteredObject<Hero>();
            TestEnvironment.CreateRegisteredObject<Equipment>();
            TestEnvironment.CreateRegisteredObject<Settlement>();
            TestEnvironment.CreateRegisteredObject<Clan>();
            TestEnvironment.CreateRegisteredObject<Town>();
            TestEnvironment.CreateRegisteredObject<CultureObject>();
            TestEnvironment.CreateRegisteredObject<MobileParty>();
            TestEnvironment.CreateRegisteredObject<PartyBase>();
        }

        [Fact]
        public void Server_Hero_Properties()
        {
            Server.ObjectManager.TryGetObject(HeroId, out Hero hero);
            TestEnvironment.AssertProperty<Hero, StaticBodyProperties>(nameof(Hero.StaticBodyProperties), body);
            TestEnvironment.AssertProperty<Hero, float>(nameof(Hero.Weight), 5f);
            TestEnvironment.AssertProperty<Hero, float>(nameof(Hero.Build), 5f);
            TestEnvironment.AssertProperty<Hero, float>(nameof(Hero.PassedTimeAtHomeSettlement), 5f);
            TestEnvironment.AssertProperty<Hero, TextObject>(nameof(Hero.EncyclopediaText), new TextObject("testText"));
            TestEnvironment.AssertProperty<Hero, bool>(nameof(Hero.IsFemale), true);
            TestEnvironment.AssertReferenceProperty<Hero, Equipment>(nameof(Hero._battleEquipment));
            TestEnvironment.AssertReferenceProperty<Hero, Equipment>(nameof(Hero._civilianEquipment));
            TestEnvironment.AssertReferenceProperty<Hero, Equipment>(nameof(Hero._stealthEquipment));
            TestEnvironment.AssertProperty<Hero, CampaignTime>(nameof(Hero.CaptivityStartTime), new CampaignTime(111));
            TestEnvironment.AssertProperty<Hero, FormationClass>(nameof(Hero.PreferredUpgradeFormation), FormationClass.Infantry, defaultValue: hero.PreferredUpgradeFormation);
            TestEnvironment.AssertProperty<Hero, Hero.CharacterStates>(nameof(Hero.HeroState), Hero.CharacterStates.Prisoner, defaultValue: hero.HeroState);
            TestEnvironment.AssertProperty<Hero, bool>(nameof(Hero.IsMinorFactionHero), true);
            //TestEnvironment.AssertReferenceProperty<Hero, IssueBase>(nameof(Hero.Issue));
            TestEnvironment.AssertReferenceProperty<Hero, Clan>(nameof(Hero.CompanionOf));
            TestEnvironment.AssertProperty<Hero, Occupation>(nameof(Hero.Occupation), Occupation.Merchant);
            TestEnvironment.AssertProperty<Hero, KillCharacterAction.KillCharacterActionDetail>(nameof(Hero.DeathMark), KillCharacterAction.KillCharacterActionDetail.Murdered);
            TestEnvironment.AssertReferenceProperty<Hero, Hero>(nameof(Hero.DeathMarkKillerHero));
            TestEnvironment.AssertReferenceProperty<Hero, Settlement>(nameof(Hero.LastKnownClosestSettlement));
            TestEnvironment.AssertProperty<Hero, int>(nameof(Hero.HitPoints), 5, defaultValue: hero.HitPoints);
            TestEnvironment.AssertProperty<Hero, long>(nameof(Hero.LastExaminedLogEntryID), 50);
            TestEnvironment.AssertReferenceProperty<Hero, Clan>(nameof(Hero.Clan));
            TestEnvironment.AssertReferenceProperty<Hero, Clan>(nameof(Hero.SupporterOf));
            TestEnvironment.AssertReferenceProperty<Hero, Town>(nameof(Hero.GovernorOf));
            TestEnvironment.AssertReferenceProperty<Hero, MobileParty>(nameof(Hero.PartyBelongedTo));
            TestEnvironment.AssertReferenceProperty<Hero, PartyBase>(nameof(Hero.PartyBelongedToAsPrisoner));
            TestEnvironment.AssertReferenceProperty<Hero, Settlement>(nameof(Hero.StayingInSettlement));
            TestEnvironment.AssertProperty<Hero, bool>(nameof(Hero.IsKnownToPlayer), true);
            TestEnvironment.AssertProperty<Hero, bool>(nameof(Hero.HasMet), true);
            TestEnvironment.AssertProperty<Hero, CampaignTime>(nameof(Hero.LastMeetingTimeWithPlayer), new CampaignTime(1351));
            TestEnvironment.AssertReferenceProperty<Hero, Settlement>(nameof(Hero.BornSettlement));
            TestEnvironment.AssertProperty<Hero, int>(nameof(Hero.Gold), 5);
            // BannerItem: EquipmentElement.ToString() NullRefs when building assertion error message; skip for now
            //TestEnvironment.AssertProperty<Hero, EquipmentElement>(nameof(Hero.BannerItem), new EquipmentElement(), defaultValue: hero.BannerItem);
            TestEnvironment.AssertProperty<Hero, int>(nameof(Hero.RandomValue), 5, defaultValue: hero.RandomValue);
            TestEnvironment.AssertReferenceProperty<Hero, Hero>(nameof(Hero.Father));
            TestEnvironment.AssertReferenceProperty<Hero, Hero>(nameof(Hero.Mother));
            TestEnvironment.AssertReferenceProperty<Hero, Hero>(nameof(Hero.Spouse));
        }

        [Fact]
        public void Server_Hero_Fields()
        {
            // Hero._health is initialized to 100 in the constructor
            TestEnvironment.AssertField<Hero, int>(nameof(Hero._health), 5, defaultValue: 100);
            // Hero.Culture is initialized by HeroCreator.CreateSpecialHero(); clear it first so the pre-check passes
            Server.ObjectManager.TryGetObject<Hero>(HeroId, out var hero);
            HarmonyLib.AccessTools.Field(typeof(Hero), nameof(Hero.Culture)).SetValue(hero, null);
            TestEnvironment.AssertReferenceField<Hero, CultureObject>(nameof(Hero.Culture));
        }
    }
}