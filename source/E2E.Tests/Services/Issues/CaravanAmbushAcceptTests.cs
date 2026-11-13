using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Quests.CaravanAmbush;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace E2E.Tests.Services.Issues;

public class CaravanAmbushAcceptTests
{
    private static string caravanId = null!;
    private static string banditId = null!;
    private static Hideout hideout = null!;
    private static readonly List<(object Container, Hero Hero, MobileParty Party)> spawns = new();
    private readonly ITestOutputHelper output;

    public CaravanAmbushAcceptTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void RealAcceptKeepsTheQuestOnItsOwnerAndSpawnsOnlyOnTheServer()
    {
        using var environment = new E2ETestEnvironment(output);
        var server = environment.Server;
        var owner = environment.Clients.First();
        var other = environment.Clients.Last();
        var townId = environment.CreateRegisteredObject<Settlement>();
        var townComponentId = environment.CreateRegisteredObject<Town>();
        var targetId = environment.CreateRegisteredObject<Settlement>();
        var targetTownId = environment.CreateRegisteredObject<Town>();
        var scenario = new QuestScenario(environment, (instance, giver) =>
            new Issue(giver, instance.GetRegisteredObject<Settlement>(targetId)));
        caravanId = environment.CreateRegisteredObject<MobileParty>();
        banditId = environment.CreateRegisteredObject<MobileParty>();

        foreach (var instance in new[] { server, owner, other })
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                    var giver = instance.GetRegisteredObject<Hero>(scenario.NotableId);
                    var town = instance.GetRegisteredObject<Settlement>(townId);
                    town.SetSettlementComponent(instance.GetRegisteredObject<Town>(townComponentId));
                    instance.GetRegisteredObject<Settlement>(targetId).SetSettlementComponent(
                        instance.GetRegisteredObject<Town>(targetTownId));
                    giver.StayingInSettlement = town;
                    giver.Occupation = Occupation.Merchant;
                    var troop = GameObjectCreator.CreateInitializedObject<CharacterObject>();
                    var culture = giver.Culture;
                    culture.BasicTroop = troop;
                    culture.CaravanMaster = troop;
                    var template = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();
                    culture.CaravanPartyTemplates = new MBList<PartyTemplateObject> { template };
                    giver.OwnedCaravans.Add(GameObjectCreator.CreateInitializedObject<CaravanPartyComponent>());
                    foreach (var player in new[] { scenario.PlayerA, scenario.PlayerB })
                    {
                        var hero = instance.GetRegisteredObject<Hero>(player.HeroId);
                        var party = instance.GetRegisteredObject<MobileParty>(player.MobilePartyId);
                        hero._partyBelongedTo = party;
                        party.MemberRoster.AddToCounts(troop, 40);
                        if ((instance == owner && player == scenario.PlayerA) ||
                            (instance == other && player == scenario.PlayerB))
                        {
                            Campaign.Current.MainParty = party;
                            Game.Current.PlayerTroop = hero.CharacterObject;
                            Campaign.Current.PlayerDefaultFaction = hero.Clan;
                        }
                    }
                    foreach (var id in new[] { "fish", "grain", "butter", "sumpter_horse" })
                        MBObjectManager.Instance.RegisterPresumedObject(new ItemObject(id));
                }
            });
        }
        server.Call(() =>
        {
            using (new AllowedThread())
            {
                var bandits = GameObjectCreator.CreateInitializedObject<Clan>();
                bandits.StringId = "looters";
                bandits.IsBanditFaction = true;
                Campaign.Current.CampaignObjectManager.AddClan(bandits);
                hideout = GameObjectCreator.CreateInitializedObject<Hideout>();
                GameObjectCreator.CreateInitializedObject<Settlement>().SetSettlementComponent(hideout);
            }
        });

        // Keep native acceptance intact; replace world factories and map speed that need the running map scene.
        var harmony = new Harmony($"caravan-accept-world-{Guid.NewGuid()}");
        spawns.Clear();
        try
        {
            harmony.Patch(AccessTools.Method(typeof(CaravanPartyComponent), nameof(CaravanPartyComponent.CreateCaravanParty)),
                prefix: new HarmonyMethod(typeof(CaravanAmbushAcceptTests), nameof(CreateCaravan)));
            harmony.Patch(AccessTools.Method(typeof(BanditPartyComponent), nameof(BanditPartyComponent.CreateBanditParty)),
                prefix: new HarmonyMethod(typeof(CaravanAmbushAcceptTests), nameof(CreateBandits)));
            harmony.Patch(AccessTools.Method(typeof(SettlementHelper), nameof(SettlementHelper.FindNearestHideoutToMobileParty)),
                prefix: new HarmonyMethod(typeof(CaravanAmbushAcceptTests), nameof(FindHideout)));
            harmony.Patch(AccessTools.PropertyGetter(typeof(MobileParty), nameof(MobileParty.Speed)),
                prefix: new HarmonyMethod(typeof(CaravanAmbushAcceptTests), nameof(ReadSpeed)));
            using var matchSpeed = new MethodCallRecorder(AccessTools.Method(typeof(MobilePartyHelper), nameof(MobilePartyHelper.TryMatchPartySpeedWithItemWeight)));

            scenario.Accept(owner);
            owner.Call(() => ((Quest)scenario.QuestOf(owner)).OnQuestAccepted());

            Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
            Assert.Equal(2, spawns.Count);
            Assert.All(spawns, call => Assert.Same(server.Container, call.Container));
            server.Call(() =>
            {
                Assert.All(spawns, call =>
                {
                    Assert.Same(server.GetRegisteredObject<Hero>(scenario.OwnerHeroId), call.Hero);
                    Assert.Same(server.GetRegisteredObject<MobileParty>(scenario.OwnerPartyId), call.Party);
                });
                var quest = Assert.IsType<Quest>(scenario.QuestOf(server));
                Assert.Same(server.GetRegisteredObject<MobileParty>(caravanId), quest._caravanParty);
                Assert.Same(server.GetRegisteredObject<MobileParty>(banditId), quest._banditParty);
                Assert.DoesNotContain(quest, Campaign.Current.QuestManager.Quests);
            });
            owner.Call(() =>
            {
                var quest = Assert.IsType<Quest>(scenario.QuestOf(owner));
                Assert.Contains(quest, Campaign.Current.QuestManager.Quests);
                Assert.Equal("S4kpdrgw", Assert.Single(quest.JournalEntries).LogText.GetID());
            });
            other.Call(() => Assert.Null(other.GetRegisteredObject<Hero>(scenario.NotableId).Issue.IssueQuest));
            Assert.Single(owner.InternalMessages.GetMessages<NetworkCaravanAmbushProgress>());
            Assert.Empty(other.InternalMessages.GetMessages<NetworkCaravanAmbushProgress>());
            Assert.Single(server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            spawns.Clear();
            hideout = null!;
        }
    }

    private static bool CreateCaravan(ref MobileParty __result) => SupplyParty(caravanId, out __result);
    private static bool CreateBandits(ref MobileParty __result) => SupplyParty(banditId, out __result);

    private static bool SupplyParty(string id, out MobileParty party)
    {
        Assert.True(ContainerProvider.TryGetContainer(out var container));
        spawns.Add((container, Hero.MainHero, MobileParty.MainParty));
        Assert.True(ContainerProvider.TryResolve<IObjectManager>(out var objects));
        Assert.True(objects.TryGetObject(id, out party));
        return false;
    }

    private static bool FindHideout(ref Hideout __result)
    {
        __result = hideout;
        return false;
    }

    private static bool ReadSpeed(ref float __result)
    {
        __result = 5f;
        return false;
    }
}
