using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Services.Issues;
using GameInterface.CoopSessionData;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.MapEvents.Interfaces;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using Moq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>Checks what the server runs when a client retreats from a field battle by leaving soldiers behind.</summary>
public class LeaveSoldiersBehindDesertionTests : MapEventTestBase
{
    private const int SacrificedTroops = 3;
    private const string ValorId = "desertion_valor";

    private readonly QuestScenario scenario;

    public LeaveSoldiersBehindDesertionTests(ITestOutputHelper output) : base(output)
    {
        scenario = new QuestScenario(TestEnvironment);
    }

    [Fact]
    public void RetreatRunsTheDesertionListenerOnlyOnTheRetreatingPlayersQuest()
    {
        var retreatingQuest = AttachListenerQuest(scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA);
        var otherNotableId = scenario.AddIssue();
        var otherQuest = AttachListenerQuest(otherNotableId, $"issue_scenario_{otherNotableId}", QuestScenario.ControllerB);

        RetreatFromFieldBattle();

        Server.Call(() =>
        {
            var hero = Server.GetRegisteredObject<Hero>(scenario.OwnerHeroId);
            var desertion = Assert.Single(retreatingQuest.Desertions);
            Assert.Equal(SacrificedTroops, desertion.Count);
            Assert.Same(hero, desertion.MainHero);
            Assert.Same(hero.PartyBelongedTo, desertion.MainParty);
            Assert.Empty(otherQuest.Desertions);
        });
    }

    [Fact]
    public void RetreatAppliesTacticsXpAndValorLossOnce()
    {
        var tacticsUsed = new List<(MobileParty Party, float Xp)>();
        var skills = new Mock<ISkillLevelingManager>();
        skills.Setup(manager => manager.OnTacticsUsed(It.IsAny<MobileParty>(), It.IsAny<float>()))
            .Callback<MobileParty, float>((party, xp) => tacticsUsed.Add((party, xp)));
        Server.Call(() => Campaign.Current.SkillLevelingManager = skills.Object);

        RetreatFromFieldBattle();

        Server.Call(() =>
        {
            var hero = Server.GetRegisteredObject<Hero>(scenario.OwnerHeroId);
            var tactics = Assert.Single(tacticsUsed);
            Assert.Same(hero.PartyBelongedTo, tactics.Party);
            Assert.Equal(SacrificedTroops * 50f, tactics.Xp);

            Campaign.Current.Models.CharacterDevelopmentModel.GetTraitLevelForTraitXp(hero, DefaultTraits.Valor, -30, out _, out var expectedValorXp);
            var traitXp = Server.Resolve<ICoopSessionProvider>().CoopSession.PlayerTraitData.PlayerTraitXp;
            Assert.Equal(expectedValorXp, traitXp[scenario.OwnerHeroId][ValorId]);
            Assert.Equal(0, traitXp[scenario.OtherHeroId][ValorId]);
        });
    }

    private void RetreatFromFieldBattle()
    {
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var handle = instance.Resolve<TestNetworkRouter>().GetOrCreateFixtureHandle(ValorId);
                Assert.True(instance.ObjectManager.AddExisting(ValorId, DefaultTraits.Valor, handle));
            });
        }

        Server.Call(() =>
        {
            using (new AllowedThread())
            {
                foreach (var player in new[] { scenario.PlayerA, scenario.PlayerB })
                {
                    Server.GetRegisteredObject<Hero>(player.HeroId)._partyBelongedTo = Server.GetRegisteredObject<MobileParty>(player.MobilePartyId);
                }
            }

            var traitXp = Server.Resolve<ICoopSessionProvider>().CoopSession.PlayerTraitData.PlayerTraitXp;
            traitXp[scenario.OwnerHeroId] = new Dictionary<string, int> { [ValorId] = 0 };
            traitXp[scenario.OtherHeroId] = new Dictionary<string, int> { [ValorId] = 0 };

            Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(
                new CampaignBehaviorBase[] { new EncounterGameMenuBehavior() }));

            // The server registers every campaign behavior's events, PlayerVariablesBehavior's listener would apply the xp and trait again
            new PlayerVariablesBehavior().RegisterEvents();
        });

        var battle = CreateServerMapEvent();
        JoinServerPartyToSide(battle.MapEventId, scenario.OwnerPartyId, BattleSideEnum.Attacker);

        // Removing troops needs a seeded roster, and the sacrifice count needs live strength models
        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(EncounterGameMenuBehavior), nameof(EncounterGameMenuBehavior.SacrificeTroopsWithRatio)))
            .ToList();

        using var sacrifice = new FixedTroopSacrifice();
        scenario.ClientA.Call(() => scenario.ClientA.Resolve<INetwork>().SendAll(
            new NetworkPlayerLeftSoldiersBehind(scenario.OwnerHeroId, scenario.OwnerPartyId)), disabledMethods);
    }

    private DesertionListenerQuest AttachListenerQuest(string giverId, string issueId, string controllerId)
    {
        DesertionListenerQuest quest = null!;

        Server.Call(() =>
        {
            var giver = Server.GetRegisteredObject<Hero>(giverId);
            quest = new DesertionListenerQuest(giver);
            giver.Issue.IssueQuest = quest;
            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(giverId, issueId, controllerId));
        });

        return quest;
    }

    /// <summary>Records each desertion with the main hero and party it ran as.</summary>
    private sealed class DesertionListenerQuest : UnstartedQuest
    {
        public List<(int Count, Hero MainHero, MobileParty MainParty)> Desertions { get; } = new();

        public DesertionListenerQuest(Hero giver) : base("desertion_listener_quest", giver)
        {
        }

        // Vanilla's quest listener for PlayerDesertedBattleEvent has this name
        private void OnPlayerDeserted(int count) => Desertions.Add((count, Hero.MainHero, MobileParty.MainParty));
    }

    private sealed class FixedTroopSacrifice : IDisposable
    {
        private static readonly MethodInfo Model = AccessTools.Method(
            typeof(DefaultTroopSacrificeModelInterface),
            nameof(DefaultTroopSacrificeModelInterface.GetNumberOfTroopsSacrificedForTryingToGetAway));

        private readonly Harmony harmony = new($"fixed-troop-sacrifice-{Guid.NewGuid()}");

        public FixedTroopSacrifice()
        {
            harmony.Patch(Model, prefix: new HarmonyMethod(typeof(FixedTroopSacrifice), nameof(Sacrifice)));
        }

        public void Dispose() => harmony.Unpatch(Model, HarmonyPatchType.Prefix, harmony.Id);

        private static bool Sacrifice(ref int __result)
        {
            __result = SacrificedTroops;
            return false;
        }
    }
}
