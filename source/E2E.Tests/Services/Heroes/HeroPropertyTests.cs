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
    public class HeroPropertyTests : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }

        EnvironmentInstance Server => TestEnvironment.Server;

        IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

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

        public HeroPropertyTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void Server_Sync_Hero()
        {
            var server = TestEnvironment.Server;
            Hero hero = null;
            Hero newHero = null;

            server.Call(() =>
            {
                hero = GameObjectCreator.CreateInitializedObject<Hero>();
                newHero = GameObjectCreator.CreateInitializedObject<Hero>();
                Clan newClan = GameObjectCreator.CreateInitializedObject<Clan>();
                Settlement newSettlement = GameObjectCreator.CreateInitializedObject<Settlement>();
                Town newTown = GameObjectCreator.CreateInitializedObject<Town>();
                MobileParty newMobileParty = GameObjectCreator.CreateInitializedObject<MobileParty>();

                Equipment newBattleEquipment = GameObjectCreator.CreateInitializedObject<Equipment>();
                Equipment newCivEquipment = GameObjectCreator.CreateInitializedObject<Equipment>();
                BettingFraudIssue newIssue = new BettingFraudIssue(hero);

                hero.StaticBodyProperties = body;
                hero.Weight = newFloat;
                hero.Build = newFloat;
                hero.PassedTimeAtHomeSettlement = newFloat;
                hero.EncyclopediaText = newText;
                hero.IsFemale = true;
                hero._battleEquipment = newBattleEquipment;
                hero._civilianEquipment = newCivEquipment;
                hero.CaptivityStartTime = newCampaignTime;
                hero.PreferredUpgradeFormation = newFormation;
                hero.HeroState = newCharState;
                hero.IsMinorFactionHero = true;
                hero.CompanionOf = newClan;
                hero.Occupation = newOccupation;
                hero.DeathMark = newKillAction;
                hero.DeathMarkKillerHero = newHero;
                hero.LastKnownClosestSettlement = newSettlement;
                hero.DeathDay = newCampaignTime;
                hero.LastExaminedLogEntryID = newLong;
                hero.Clan = newClan;
                hero.SupporterOf = newClan;
                hero.GovernorOf = newTown;
                hero.PartyBelongedTo = newMobileParty;
                hero.PartyBelongedToAsPrisoner = newMobileParty.Party;
                hero.StayingInSettlement = newSettlement;
                hero.IsKnownToPlayer = true;
                hero.HasMet = true;
                hero.LastMeetingTimeWithPlayer = newCampaignTime;
                hero.BornSettlement = newSettlement;
                hero.Gold = newInt;
                hero.RandomValue = newInt;
                hero.Father = newHero;
                hero.Mother = newHero;
                hero.Spouse = newHero;

                Assert.Equal(body, hero.StaticBodyProperties);

                Assert.True(server.ObjectManager.TryGetId(hero, out HeroId));
                Assert.True(server.ObjectManager.TryGetId(newHero, out OtherHeroId));
                Assert.True(server.ObjectManager.TryGetId(newSettlement, out SettlementId));
                Assert.True(server.ObjectManager.TryGetId(newClan, out ClanId));
                Assert.True(server.ObjectManager.TryGetId(newTown, out TownId));
                Assert.True(server.ObjectManager.TryGetId(newMobileParty, out MobilePartyId));
                Assert.True(server.ObjectManager.TryGetId(newCivEquipment, out CivEquipmentId));
                Assert.True(server.ObjectManager.TryGetId(newBattleEquipment, out BattleEquipmentId));
            });

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(HeroId, out var clientHero));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(OtherHeroId, out var clientOtherHero));
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(SettlementId, out var clientSettlement));
                Assert.True(client.ObjectManager.TryGetObject<Clan>(ClanId, out var clientClan));
                Assert.True(client.ObjectManager.TryGetObject<Town>(TownId, out var clientTown));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientMobileParty));
                Assert.True(client.ObjectManager.TryGetObject<Equipment>(CivEquipmentId, out var clientCivEquipment));
                Assert.True(client.ObjectManager.TryGetObject<Equipment>(BattleEquipmentId, out var clientBattleEquipment));

                Assert.Equal(clientCivEquipment, clientHero._civilianEquipment);
                Assert.Equal(clientBattleEquipment, clientHero._battleEquipment);
                Assert.Equal(clientClan, clientHero.Clan);
                Assert.Equal(clientOtherHero, clientHero.DeathMarkKillerHero);
                Assert.Equal(clientSettlement, clientHero.LastKnownClosestSettlement);
                Assert.Equal(clientClan, clientHero.CompanionOf);
                Assert.Equal(clientClan, clientHero.SupporterOf);
                Assert.Equal(clientTown, clientHero.GovernorOf);
                Assert.Equal(clientMobileParty, clientHero.PartyBelongedTo);
                Assert.Equal(clientMobileParty.Party, clientHero.PartyBelongedToAsPrisoner);
                Assert.Equal(clientSettlement, clientHero.StayingInSettlement);
                Assert.Equal(clientSettlement, clientHero.BornSettlement);
                Assert.Equal(clientOtherHero, clientHero.Mother);
                Assert.Equal(clientOtherHero, clientHero.Father);
                Assert.Equal(clientOtherHero, clientHero.Spouse);
                Assert.Equal(body, clientHero.StaticBodyProperties);
                Assert.Equal(newFloat, clientHero.Weight);
                Assert.Equal(newFloat, clientHero.Build);
                Assert.Equal(newFloat, clientHero.PassedTimeAtHomeSettlement);
                Assert.Equal(newText.Value, clientHero.EncyclopediaText.Value);
                Assert.True(clientHero.IsFemale);
                Assert.Equal(newCampaignTime, clientHero.CaptivityStartTime);
                Assert.Equal(newFormation, clientHero.PreferredUpgradeFormation);
                Assert.Equal(newCharState, clientHero.HeroState);
                Assert.True(clientHero.IsMinorFactionHero);
                Assert.Equal(newOccupation, clientHero.Occupation);
                Assert.Equal(newKillAction, clientHero.DeathMark);
                Assert.Equal(newCampaignTime, clientHero.DeathDay);
                Assert.Equal(newLong, clientHero.LastExaminedLogEntryID);
                Assert.True(clientHero.IsKnownToPlayer);
                Assert.True(clientHero.HasMet);
                Assert.Equal(newCampaignTime, clientHero.LastMeetingTimeWithPlayer);
                Assert.Equal(newInt, clientHero.Gold);
                Assert.Equal(newInt, clientHero.RandomValue);
            }
        }
    }
}
