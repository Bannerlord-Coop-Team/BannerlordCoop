using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Heroes.HeirSelection.Interfaces;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using GameInterface.Services.Traits.Messages;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

namespace E2E.Tests.Services.Issues;

public class HeadmanHerdSuccessionTests
{
    private readonly ITestOutputHelper output;

    public HeadmanHerdSuccessionTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void NativeHeirSelectionCancelsBeforeInventoryInheritanceAndPreservesSurplus()
    {
        using var herd = new HeadmanNeedsToDeliverAHerdTests(output);
        var scenario = herd.scenario;
        var instances = new[] { scenario.Server, scenario.ClientA, scenario.ClientB };
        var successorPartyId = herd.environment.CreateRegisteredObject<MobileParty>();
        var heirId = string.Empty;
        scenario.Server.Call(() => Assert.True(scenario.Server.ObjectManager.TryGetId(
            Object<MobileParty>(scenario.Server, successorPartyId).LeaderHero, out heirId)));
        var modifierId = string.Empty;
        scenario.Server.Call(() => Assert.True(scenario.Server.ObjectManager.TryGetId(new ItemModifier(), out modifierId)));
        foreach (var instance in instances)
            instance.Call(() =>
            {
                new JournalLogsCampaignBehavior().RegisterEvents();
                new HeirSelectionCampaignBehavior().RegisterEvents();
                using var setup = new AllowedThread();
                if (instance != scenario.Server)
                {
                    Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(scenario.PlayerA));
                    Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(scenario.PlayerB));
                }
                var oldHero = Object<Hero>(instance, scenario.OwnerHeroId);
                oldHero.Occupation = Occupation.Lord;
                oldHero.SetBirthDay(CampaignTime.YearsFromNow(-45));
                oldHero.Clan.SetLeader(oldHero);
                oldHero.Clan._banner = new Banner();
                var heir = Object<Hero>(instance, heirId);
                heir.Occupation = Occupation.Lord;
                heir.SetBirthDay(CampaignTime.YearsFromNow(-20));
                heir.Father = oldHero;
                heir.Clan = oldHero.Clan;
                var party = Object<MobileParty>(instance, successorPartyId);
                heir._partyBelongedTo = party;
                Assert.Same(heir, party.LeaderHero);
                party.ItemRoster.Clear();
            });

        herd.Accept(scenario.ClientA);
        var otherGiverId = scenario.AddIssue();
        scenario.ClientB.Call(() =>
        {
            using var owner = new MainHeroSubstitutionScope(Object<Hero>(scenario.ClientB, scenario.OtherHeroId),
                Object<MobileParty>(scenario.ClientB, scenario.OtherPartyId));
            var issue = Object<Hero>(scenario.ClientB, otherGiverId).Issue;
            issue.StartIssueWithQuest();
            Assert.IsType<Quest>(issue.IssueQuest).QuestAcceptedConsequences();
        });
        herd.environment.FlushCoalescer();
        var otherInventory = new Dictionary<EnvironmentInstance, (EquipmentElement, int)[]>();
        var otherRoster = new Dictionary<EnvironmentInstance, (string, int, int, int)[]>();
        var otherJournal = new Dictionary<EnvironmentInstance, JournalLog[]>();
        foreach (var instance in instances)
            instance.Call(() =>
            {
                otherInventory[instance] = Object<MobileParty>(instance, scenario.OtherPartyId).ItemRoster
                    .Select(element => (element.EquipmentElement, element.Amount)).ToArray();
                otherRoster[instance] = Object<MobileParty>(instance, scenario.OtherPartyId).MemberRoster.GetTroopRoster()
                    .Select(element => (element.Character.StringId, element.Number, element.WoundedNumber, element.Xp)).ToArray();
                otherJournal[instance] = Object<Hero>(instance, otherGiverId).Issue.IssueQuest?.JournalEntries.ToArray() ?? Array.Empty<JournalLog>();
            });
        var bystanderGold = 0;
        var totalGold = 0;
        var livestockId = string.Empty;
        scenario.Server.Call(() =>
        {
            var quest = Assert.IsType<Quest>(Object<Hero>(scenario.Server, scenario.NotableId).Issue.IssueQuest);
            Assert.Equal(10, quest._animalCountToDeliver);
            Assert.True(scenario.Server.ObjectManager.TryGetId(quest._herdTypeToDeliver, out livestockId));
            var party = Object<MobileParty>(scenario.Server, scenario.OwnerPartyId);
            party.ItemRoster.AddToCounts(quest._herdTypeToDeliver, -party.ItemRoster.GetItemNumber(quest._herdTypeToDeliver));
            party.ItemRoster.AddToCounts(quest._herdTypeToDeliver, 3);
            party.ItemRoster.AddToCounts(new EquipmentElement(quest._herdTypeToDeliver,
                Object<ItemModifier>(scenario.Server, modifierId)), 30);
            bystanderGold = Object<Hero>(scenario.Server, scenario.OtherHeroId).Gold;
            totalGold = Object<Hero>(scenario.Server, scenario.OwnerHeroId).Gold + Object<Hero>(scenario.Server, heirId).Gold;
            var oldHero = Object<Hero>(scenario.Server, scenario.OwnerHeroId);
            scenario.Server.Resolve<IHeirSelectionCampaignBehaviorInterface>().PrepareSuccession(oldHero);
            // The existing succession E2E seam supplies death state; the real selection message drives every subsequent action.
            oldHero.AddDeathMark(null, KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge);
            oldHero.ChangeState(Hero.CharacterStates.Dead);
            oldHero._partyBelongedTo = null;
            party.IsActive = false;
        });
        herd.environment.FlushCoalescer();

        // Only map rendering is excluded. Run the installed character-change callbacks in their real dispatcher order.
        var presentation = new Harmony($"herd-succession-{Guid.NewGuid()}");
        presentation.Patch(AccessTools.Method(typeof(ChangePlayerCharacterAction), nameof(ChangePlayerCharacterAction.Apply)),
            prefix: new HarmonyMethod(typeof(HeadmanHerdSuccessionTests), nameof(ApplyCharacterChangeCallbacks)) { priority = Priority.First });
        try
        {
            scenario.Server.SimulateMessage(scenario.ClientA.NetPeer,
                scenario.ClientA.SerializeForWire(new NetworkHeirSelectionOver(scenario.OwnerHeroId, heirId)));
            scenario.Server.PumpGameThread();
            herd.environment.FlushCoalescer();
            scenario.ClientA.PumpGameThread();
            scenario.ClientB.PumpGameThread();

            foreach (var instance in instances)
                instance.Call(() =>
                {
                    Assert.True(instance.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var player));
                    Assert.Equal(heirId, player.HeroId);
                    Assert.Equal(successorPartyId, player.MobilePartyId);
                    Assert.False(instance.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out _));
                    Assert.Null(Object<Hero>(instance, scenario.NotableId).Issue);
                });
            Assert.Equal(IssueOutcome.QuestCancel,
                Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
            Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkChangePlayerCharacterAfterHeirSelection>());
            Assert.Single(scenario.ClientA.InternalMessages.GetMessages<PlayerCharacterChangedAfterHeirSelection>());

            var checks = new List<Action>();
            foreach (var instance in instances)
            {
                checks.Add(() => instance.Call(() =>
                {
                    var successor = Object<MobileParty>(instance, successorPartyId).ItemRoster;
                    var livestock = Object<ItemObject>(instance, livestockId);
                    var total = successor.Where(element => element.EquipmentElement.Item == livestock).Sum(element => element.Amount);
                    output.WriteLine("{0}: successor sheep={1}, expected=23", instance.GetType().Name, total);
                    Assert.Equal(23, total);
                    var modified = Assert.Single(successor.Where(element => element.EquipmentElement.Item == livestock));
                    output.WriteLine("{0}: successor sheep={1}, modified={2}", instance.GetType().Name, modified.Amount,
                        modified.EquipmentElement.ItemModifier != null);
                    Assert.Equal(23, modified.Amount);
                    Assert.Same(Object<ItemModifier>(instance, modifierId), modified.EquipmentElement.ItemModifier);
                    Assert.Equal(5, successor.GetItemNumber(DefaultItems.Grain));
                }));
                checks.Add(() => instance.Call(() =>
                {
                    Assert.Equal(otherInventory[instance], Object<MobileParty>(instance, scenario.OtherPartyId).ItemRoster
                        .Select(element => (element.EquipmentElement, element.Amount)).ToArray());
                    Assert.Equal(otherRoster[instance], Object<MobileParty>(instance, scenario.OtherPartyId).MemberRoster.GetTroopRoster()
                        .Select(element => (element.Character.StringId, element.Number, element.WoundedNumber, element.Xp)).ToArray());
                    var other = Object<Hero>(instance, otherGiverId).Issue;
                    Assert.True(other.IsSolvingWithQuest);
                    AssertHistory(otherJournal[instance], other.IssueQuest?.JournalEntries ?? Enumerable.Empty<JournalLog>());
                    Assert.Equal(bystanderGold, Object<Hero>(instance, scenario.OtherHeroId).Gold);
                    Assert.Equal(totalGold, Object<Hero>(instance, scenario.OwnerHeroId).Gold + Object<Hero>(instance, heirId).Gold);
                    Assert.Empty(instance.NetworkSentMessages.GetMessages<NetworkPlayerTraitXpAdded>());
                }));
            }
            checks.Add(() =>
            {
                JournalLog[] authoritative = null!;
                scenario.Server.Call(() => authoritative = Archive().GetEntries().ToArray());
                scenario.ClientA.Call(() =>
                {
                    var archive = Archive();
                    Assert.True(archive.IsEnded());
                    Assert.Equal(IssueBase.IssueUpdateDetails.IssueCancel, archive._lastIssueStatus);
                    AssertHistory(authoritative, archive.GetEntries());
                });
                scenario.ClientB.Call(() => Assert.Empty(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                    entry => entry._relatedObjectIds.Contains(QuestScenario.IssueId))));
            });
            checks.Add(() =>
            {
                scenario.Server.Call(() => CampaignEventDispatcher.Instance.HourlyTick());
                herd.environment.FlushCoalescer();
                Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
            });
            Assert.Multiple(checks.ToArray());
        }
        finally { presentation.UnpatchAll(presentation.Id); }
    }

    public static bool ApplyCharacterChangeCallbacks(Hero hero)
    {
        using var scope = new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo);
        CampaignEventDispatcher.Instance.OnPlayerCharacterChanged(hero.Father, hero, hero.PartyBelongedTo, true);
        return false;
    }

    private static JournalLogEntry Archive() => Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
        entry => entry._relatedObjectIds.Contains(QuestScenario.IssueId)));

    private static void AssertHistory(IEnumerable<JournalLog> expected, IEnumerable<JournalLog> actual)
    {
        var left = expected.ToArray();
        var right = actual.ToArray();
        Assert.Equal(left.Length, right.Length);
        for (var index = 0; index < left.Length; index++)
        {
            Assert.Equal(left[index].LogTime, right[index].LogTime);
            Assert.Equal(left[index].CurrentProgress, right[index].CurrentProgress);
            Assert.Equal(left[index].Range, right[index].Range);
            Assert.Equal(left[index].Type, right[index].Type);
            AssertText(left[index].LogText, right[index].LogText);
            AssertText(left[index].TaskName, right[index].TaskName);
        }
    }

    private static void AssertText(TextObject? expected, TextObject? actual)
    {
        if (expected == null) { Assert.Null(actual); return; }
        Assert.NotNull(actual);
        Assert.Equal(expected.Value, actual.Value);
        Assert.Equal(expected.ToString(), actual.ToString());
        Assert.Equal(expected.Attributes == null, actual.Attributes == null);
        if (expected.Attributes == null) return;
        Assert.Equal(expected.Attributes.Keys.OrderBy(key => key), actual.Attributes.Keys.OrderBy(key => key));
        foreach (var attribute in expected.Attributes)
            if (attribute.Value is TextObject text)
                AssertText(text, Assert.IsType<TextObject>(actual.Attributes[attribute.Key]));
            else
                Assert.Equal(attribute.Value, actual.Attributes[attribute.Key]);
    }

    private static T Object<T>(EnvironmentInstance instance, string id) where T : class
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }
}
