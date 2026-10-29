using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

public class PregnancyCampaignBehaviorInterfaceTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;

    public PregnancyCampaignBehaviorInterfaceTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    // The girl case is the control, she uses the mother's template which has a base name
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CheckOffspringToDeliver_PlayerFather_DeliversTheChildAndEndsThePregnancy(bool isFemale)
    {
        var fatherId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var motherId = TestEnvironment.CreateRegisteredObject<Hero>();
        Server.Call(() => Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player("player-A", fatherId, partyId, "", ""))));
        TestEnvironment.ConnectRegisteredPlayer(TestEnvironment.Clients.First(), "player-A");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fatherId, out var father));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(motherId, out var mother));

            // _basicName isn't saved and InitializeCharactersAfterLoad only restores it on template copies,
            // so a player's character has none after the server reloads its save
            father.CharacterObject._basicName = null;
            Campaign.Current.Models.PregnancyModel = new FixedPregnancyModel(isFemale);

            var town = GameObjectCreator.CreateInitializedObject<Settlement>();
            town.SetSettlementComponent(GameObjectCreator.CreateInitializedObject<Town>());
            mother.StayingInSettlement = town;

            var behavior = new PregnancyCampaignBehavior();
            var pregnancy = new PregnancyCampaignBehavior.Pregnancy(mother, father, CampaignTime.Now);
            behavior._heroPregnancies.Add(pregnancy);
            mother.IsPregnant = true;

            Server.Resolve<IPregnancyCampaignBehaviorInterface>().CheckOffspringToDeliver(behavior, pregnancy);

            var child = Assert.Single(mother.Children);
            Assert.Same(father, child.Father);
            Assert.Equal(isFemale, child.IsFemale);
            Assert.False(string.IsNullOrEmpty(child.Name?.ToString()));
            Assert.False(string.IsNullOrEmpty(child.FirstName?.ToString()));
            Assert.Empty(behavior._heroPregnancies);
            Assert.False(mother.IsPregnant);
        });
    }

    private class FixedPregnancyModel : PregnancyModel
    {
        private readonly bool isFemale;

        public FixedPregnancyModel(bool isFemale)
        {
            this.isFemale = isFemale;
        }

        // Rolls are compared with <= or >, so values outside 0 to 1 make every roll go one way

        public override float PregnancyDurationInDays => 36f;
        public override float MaternalMortalityProbabilityInLabor => -1f;
        public override float StillbirthProbability => -1f;
        public override float DeliveringFemaleOffspringProbability => isFemale ? 2f : -1f;
        public override float DeliveringTwinsProbability => -1f;
        public override float GetDailyChanceOfPregnancyForHero(Hero hero) => 0f;
    }
}
