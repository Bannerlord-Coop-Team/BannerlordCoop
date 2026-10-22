#if DEBUG
using Common.Commands;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Commands;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

public class HeroConversationDebugCommandTests : IDisposable
{
    private static readonly MethodBase OpenConversationMethod =
        AccessTools.Method(typeof(CampaignMapConversation), nameof(CampaignMapConversation.OpenConversation));

    private readonly E2ETestEnvironment testEnvironment;
    private readonly MethodCallRecorder openConversation;
    private EnvironmentInstance Client => testEnvironment.Clients.First();

    public HeroConversationDebugCommandTests(ITestOutputHelper output)
    {
        testEnvironment = new E2ETestEnvironment(output);
        openConversation = new MethodCallRecorder(Priority.First, (MethodInfo)OpenConversationMethod);
    }

    public void Dispose()
    {
        openConversation.Dispose();
        testEnvironment.Dispose();
    }

    [Fact]
    public void Open_InATownMenu_OpensTheConversationWithAHeroInThatTown()
    {
        var townId = testEnvironment.CreateRegisteredObject<Settlement>();
        var heroId = CreateHeroStayingIn(townId);

        var result = OpenDuringEncounterAt(townId, heroId);

        Assert.True(result.Succeeded, result.Output);
        Assert.Equal(1, openConversation.CountFor(Client));
    }

    [Fact]
    public void Open_InATownMenu_RefusesAHeroInAnotherTown()
    {
        var townId = testEnvironment.CreateRegisteredObject<Settlement>();
        var otherTownId = testEnvironment.CreateRegisteredObject<Settlement>();
        var heroId = CreateHeroStayingIn(otherTownId);

        var result = OpenDuringEncounterAt(townId, heroId);

        Assert.False(result.Succeeded);
        Assert.Equal("A player encounter is already active.", result.Output);
        Assert.Equal(0, openConversation.CountFor(Client));
    }

    [Fact]
    public void Open_DuringASiegeOfTheHerosTown_IsRefused()
    {
        var townId = testEnvironment.CreateRegisteredObject<Settlement>();
        var heroId = CreateHeroStayingIn(townId);

        var result = OpenDuringEncounterAt(townId, heroId, (_, town) => town.SiegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>());

        Assert.False(result.Succeeded);
        Assert.Equal("A player encounter is already active.", result.Output);
        Assert.Equal(0, openConversation.CountFor(Client));
    }

    [Fact]
    public void Open_DuringABattleAtTheHerosTown_IsRefused()
    {
        var townId = testEnvironment.CreateRegisteredObject<Settlement>();
        var heroId = CreateHeroStayingIn(townId);

        var result = OpenDuringEncounterAt(townId, heroId, (encounter, _) => encounter._mapEvent = ObjectHelper.SkipConstructor<MapEvent>());

        Assert.False(result.Succeeded);
        Assert.Equal("A player encounter is already active.", result.Output);
        Assert.Equal(0, openConversation.CountFor(Client));
    }

    [Fact]
    public void Open_DuringAMapEncounter_RefusesAHeroOutsideAnySettlement()
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();

        var result = OpenDuringEncounterAt(null, heroId);

        Assert.False(result.Succeeded);
        Assert.Equal(0, openConversation.CountFor(Client));
    }

    [Fact]
    public void Choose_WithoutAConversation_Fails()
    {
        Client.Call(() =>
        {
            var result = Choose();

            Assert.False(result.Succeeded);
            Assert.Equal("No conversation is active.", result.Output);
        });
    }

    [Fact]
    public void Choose_WithoutAnOption_ListsTheLineAndEveryOption()
    {
        WithConversation(() =>
        {
            var result = Choose();

            Assert.True(result.Succeeded, result.Output);
            Assert.Contains("LINE: Who are you?", result.Output);
            Assert.Contains("0: [first_option] I am a trader.", result.Output);
            Assert.Contains("1: [second_option] I must leave now. (disabled)", result.Output);
            Assert.DoesNotContain("I am a trader. (disabled)", result.Output);
        });
    }

    [Fact]
    public void Choose_ADisabledOption_IsRefused()
    {
        WithConversation(() =>
        {
            var result = Choose("1");

            Assert.False(result.Succeeded);
            Assert.Equal("Option 1 is disabled.", result.Output);
            Assert.Equal(2, Campaign.Current.ConversationManager.CurOptions.Count);
        });
    }

    [Fact]
    public void Choose_ContinueWhileThePlayerHasAChoice_IsRefused()
    {
        WithConversation(() =>
        {
            var result = Choose("continue");

            Assert.False(result.Succeeded);
            Assert.Equal("Pick one of the 2 options instead.", result.Output);
            Assert.Equal(2, Campaign.Current.ConversationManager.CurOptions.Count);
        });
    }

    [Theory]
    [InlineData("2")]
    [InlineData("-1")]
    [InlineData("first_option")]
    public void Choose_WithAnUnknownOption_FailsWithoutChangingTheOptions(string option)
    {
        WithConversation(() =>
        {
            var result = Choose(option);

            Assert.False(result.Succeeded);
            Assert.Equal($"Unknown option '{option}'.", result.Output);
            Assert.Equal(2, Campaign.Current.ConversationManager.CurOptions.Count);
        });
    }

    private string CreateHeroStayingIn(string settlementId)
    {
        var heroId = testEnvironment.CreateRegisteredObject<Hero>();

        foreach (var instance in testEnvironment.Clients.Append(testEnvironment.Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

                using (new AllowedThread())
                {
                    hero.StayingInSettlement = settlement;
                }
            });
        }

        return heroId;
    }

    private CoopCommandResult OpenDuringEncounterAt(string? settlementId, string heroId, Action<PlayerEncounter, Settlement>? arrange = null)
    {
        CoopCommandResult result = null!;

        Client.Call(() =>
        {
            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            Settlement? settlement = null;
            if (settlementId != null)
            {
                Assert.True(Client.ObjectManager.TryGetObject(settlementId, out settlement));
                encounter.EncounterSettlementAux = settlement;
            }

            var previousSiege = settlement?.SiegeEvent;
            arrange?.Invoke(encounter, settlement!);
            Campaign.Current.PlayerEncounter = encounter;
            try
            {
                result = new HeroConversationDebugCommand.HeroConversationOpenCoopCommand()
                    .ProcessCommand(new CoopCommandArgsFactory().FromValues(new[] { heroId }));
            }
            finally
            {
                Campaign.Current.PlayerEncounter = null;
                if (settlement != null)
                {
                    settlement.SiegeEvent = previousSiege;
                }
            }
        }, new[] { OpenConversationMethod });

        return result;
    }

    private void WithConversation(Action assert)
    {
        Client.Call(() =>
        {
            var manager = Campaign.Current.ConversationManager;
            var previousOptions = manager.CurOptions;
            var previousText = manager._currentSentenceText;
            manager.IsConversationInProgress = true;
            manager.CurOptions = new List<ConversationSentenceOption>
            {
                new ConversationSentenceOption { Id = "first_option", Text = new TextObject("I am a trader."), IsClickable = true },
                new ConversationSentenceOption { Id = "second_option", Text = new TextObject("I must leave now."), IsClickable = false },
            };
            manager._currentSentenceText = new TextObject("Who are you?");

            try
            {
                assert();
            }
            finally
            {
                manager.IsConversationInProgress = false;
                manager.CurOptions = previousOptions;
                manager._currentSentenceText = previousText;
            }
        });
    }

    private static CoopCommandResult Choose(params string[] args) =>
        new HeroConversationDebugCommand.HeroConversationChooseCoopCommand()
            .ProcessCommand(new CoopCommandArgsFactory().FromValues(args));
}
#endif
