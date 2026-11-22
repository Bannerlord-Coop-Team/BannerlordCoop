using Common.Util;
using GameInterface.Services.MapEvents.Patches;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class PlayerEncounterVictoryTests : MapEventTestBase
{
    public PlayerEncounterVictoryTests(ITestOutputHelper output) : base(output) { }

    // R37: after a coop naval victory the ally was thanked by the other player's hero in a sea conversation.
    [Fact]
    public void PlayerVictory_ThanksOnlyTheHelpedLordsThatNoPlayerControls()
    {
        var (playerHeroId, _) = CreatePlayerHeroParty("PlayerTwo");
        var lordHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var client = Clients.First();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(lordHeroId, out var lordHero));
            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter._helpedHeroes = new List<Hero> { playerHero, lordHero };

            PlayerEncounterPatches.SkipHelpedPlayerHeroes(encounter);

            Assert.Equal(new[] { lordHero }, encounter._helpedHeroes);
        });
    }

    [Fact]
    public void PlayerVictory_BeforeTheHelpedLordsAreListed_LeavesTheListUnset()
    {
        var client = Clients.First();

        client.Call(() =>
        {
            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();

            PlayerEncounterPatches.SkipHelpedPlayerHeroes(encounter);

            Assert.Null(encounter._helpedHeroes);
        });
    }
}
