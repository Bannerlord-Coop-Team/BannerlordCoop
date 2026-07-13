using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

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
            var assertHelper = TestEnvironment.CreateAssertHelper<Hero>(HeroId);

            // Hero.Culture is initialized by HeroCreator.CreateSpecialHero(); clear it first so the pre-check passes
            Server.ObjectManager.TryGetObject<Hero>(HeroId, out var hero);
            HarmonyLib.AccessTools.Field(typeof(Hero), nameof(Hero.Culture)).SetValue(hero, null);
            TestEnvironment.AssertReferenceField<Hero, CultureObject>(nameof(Hero.Culture));
            TestEnvironment.AssertField<Hero, float>(nameof(Hero._power), 4.4f, defaultValue: hero._power);

            assertHelper.AssertPropertyOwnerField<Hero, TraitObject>(nameof(Hero._heroTraits));
            assertHelper.AssertPropertyOwnerField<Hero, PerkObject>(nameof(Hero._heroPerks));
            assertHelper.AssertPropertyOwnerField<Hero, SkillObject>(nameof(Hero._heroSkills));
            assertHelper.AssertPropertyOwnerField<Hero, CharacterAttribute>(nameof(Hero._characterAttributes));

            TestEnvironment.AssertField<Hero, float>(nameof(Hero.Level), 10, defaultValue: hero.Level);
        }

        // Calls the REAL patched game method (not a reflection-invoked intercept), so this covers the
        // PropertyOwner transpiler's IL rewrite of Hero.SetSkillValue end to end.
        [Fact]
        public void Server_Hero_SetSkillValue_PropagatesToClients()
        {
            var skillId = TestEnvironment.CreateRegisteredObject<SkillObject>();

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject(HeroId, out Hero hero));
                Assert.True(Server.ObjectManager.TryGetObject(skillId, out SkillObject skill));

                hero.SetSkillValue(skill, 42);

                Assert.Equal(42, hero.GetSkillValue(skill));
            });

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject(HeroId, out Hero hero));
                Assert.True(client.ObjectManager.TryGetObject(skillId, out SkillObject skill));
                Assert.Equal(42, hero.GetSkillValue(skill));
            }
        }

        // SetPerkValueInternal's bool->int branch defeats the transpiler, so SetPerkValuePatch routes
        // perk changes through the cached PropertyOwner intercept - this exercises that prefix for real
        [Fact]
        public void Server_Hero_SetPerkValue_PropagatesToClients()
        {
            var perkId = TestEnvironment.CreateRegisteredObject<PerkObject>();

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject(HeroId, out Hero hero));
                Assert.True(Server.ObjectManager.TryGetObject(perkId, out PerkObject perk));

                hero.SetPerkValueInternal(perk, true);

                Assert.True(hero.GetPerkValue(perk));
            });

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject(HeroId, out Hero hero));
                Assert.True(client.ObjectManager.TryGetObject(perkId, out PerkObject perk));
                Assert.True(hero.GetPerkValue(perk));
            }

            // Resetting the perk rides the same message with value 0 (vanilla removes the key)
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject(HeroId, out Hero hero));
                Assert.True(Server.ObjectManager.TryGetObject(perkId, out PerkObject perk));

                hero.SetPerkValueInternal(perk, false);

                Assert.False(hero.GetPerkValue(perk));
            });

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject(HeroId, out Hero hero));
                Assert.True(client.ObjectManager.TryGetObject(perkId, out PerkObject perk));
                Assert.False(hero.GetPerkValue(perk));
            }
        }

        // Hero.ClearSkills routes through PropertyOwner.ClearAllProperty - covers the clear message pair
        [Fact]
        public void Server_Hero_ClearSkills_PropagatesToClients()
        {
            var skillId = TestEnvironment.CreateRegisteredObject<SkillObject>();

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject(HeroId, out Hero hero));
                Assert.True(Server.ObjectManager.TryGetObject(skillId, out SkillObject skill));

                hero.SetSkillValue(skill, 17);
            });

            // The set must land on clients first, so the clear observably removes it
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject(HeroId, out Hero hero));
                Assert.True(client.ObjectManager.TryGetObject(skillId, out SkillObject skill));
                Assert.Equal(17, hero.GetSkillValue(skill));
            }

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject(HeroId, out Hero hero));
                Assert.True(Server.ObjectManager.TryGetObject(skillId, out SkillObject skill));

                hero.ClearSkills();

                Assert.Equal(0, hero.GetSkillValue(skill));
            });

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject(HeroId, out Hero hero));
                Assert.True(client.ObjectManager.TryGetObject(skillId, out SkillObject skill));
                Assert.Equal(0, hero.GetSkillValue(skill));
            }
        }
    }
}
