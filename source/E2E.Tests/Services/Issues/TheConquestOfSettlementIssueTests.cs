using Common.Util;
using E2E.Tests.Environment;
using Common.Commands;
using Common.Messaging;
using Coop.Core.Server.Services.Stances.Messages;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Heroes.Commands;
using GameInterface.Services.HeroDevelopers.Commands;
using Newtonsoft.Json.Linq;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Issues.Commands;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.TheConquestOfSettlement;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Patches;
using GameInterface.Services.Kingdoms.Commands;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Villages.Commands;
using HarmonyLib;
using Helpers;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

using Issue = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue;
using Quest = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest;

public class TheConquestOfSettlementIssueTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private readonly ITestOutputHelper output;
    private EnvironmentInstance Server => environment.Server;

    public TheConquestOfSettlementIssueTests(ITestOutputHelper output)
    {
        this.output = output;
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

#if DEBUG
    [Theory]
    [InlineData("hero")]
    [InlineData("party")]
    [InlineData("player")]
    public void MissingOwnerDiagnosticCancelsTheQuestAndRestoresTheSameRegistrations(string missing)
    {
        var created = CreateIssue();
        var heroId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var players = Server.Resolve<IPlayerManager>();
            var player = new Player("cleanup-owner", heroId, partyId, "", "");
            Assert.True(players.AddPlayer(player));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetHandle(hero, out var heroHandle));
            Assert.True(Server.ObjectManager.TryGetHandle(party, out var partyHandle));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(created.GiverId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(created.TargetId, out var target));
            var quest = new Quest(created.IssueId + "_quest", giver, target, CampaignTime.DaysFromNow(60), 20000);
            using (new AllowedThread())
            {
                giver.Issue.IssueQuest = quest;
                giver.Issue.IsTriedToSolveBefore = true;
                giver.Issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
                Campaign.Current.QuestManager.OnQuestStarted(quest);
            }
            var ownership = Server.Resolve<IIssueOwnershipRegistry>();
            ownership.SetOwner(giver, player.ControllerId);
            ownership.SetQuestOwner(quest.StringId, player.ControllerId);
            Assert.Equal(created.IssueId + "_quest", quest.StringId);
            Assert.True(quest.IsOngoing);
            Assert.Contains(quest, Campaign.Current.QuestManager.Quests);
            Assert.True(ownership.TryGetQuestOwner(quest.StringId, out var questOwner));
            Assert.Equal(player.ControllerId, questOwner);
            var result = new ConquestQuestCommands.CancelMissingOwner().ProcessCommand(
                new CoopCommandArgsFactory().FromValues(new[] { player.ControllerId, missing }));
            Assert.True(result.Succeeded, result.Output);
            Assert.False(JObject.Parse(result.Output)["during"].Value<bool>(missing + "Available"));
            Assert.True(quest.IsFinalized);
            Assert.Null(giver.Issue);
            Assert.True(players.TryGetPlayer(player.ControllerId, out var restored));
            Assert.Same(player, restored);
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroHandle, out var restoredHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyHandle, out var restoredParty));
            Assert.Same(hero, restoredHero);
            Assert.Same(party, restoredParty);
        });
    }

    [Fact]
    public void ExpiryObservationKeepsTheNativeRandomDrawAndItsValue()
    {
        var method = AccessTools.Method(typeof(IssueManager), nameof(IssueManager.DailyTick));
        var original = PatchProcessor.GetOriginalInstructions(method).ToList();
        var observed = ConquestQuestCommands.IssueExpiryTimingPatch.Transpiler(original, method).ToList();
        var random = AccessTools.PropertyGetter(typeof(MBRandom), nameof(MBRandom.RandomFloat));
        Assert.True(ConquestQuestCommands.IssueExpiryTimingPatch.ObservationAvailable);
        Assert.Equal(original.Count + 2, observed.Count);
        Assert.Single(observed.Where(code => code.Calls(random)));
        Assert.Single(observed.Where(code => code.Calls(AccessTools.Method(
            typeof(ConquestQuestCommands.IssueExpiryTimingPatch), nameof(ConquestQuestCommands.IssueExpiryTimingPatch.ObserveRoll)))));
        var created = CreateIssue();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(created.GiverId, out var giver));
            var history = ConquestQuestCommands.GetTickHistory(Campaign.Current.IssueManager);

            Assert.Equal(0.2f, ConquestQuestCommands.IssueExpiryTimingPatch.ObserveRoll(0.2f, giver.Issue));
            Assert.Equal(0.9f, ConquestQuestCommands.IssueExpiryTimingPatch.ObserveRoll(0.9f, giver.Issue));
            var samples = history.ExpiryRolls.Reverse().Take(2).Reverse().Select(JObject.FromObject).ToArray();
            Assert.Equal(2, samples.Length);
            Assert.All(samples, sample => Assert.Equal(giver.Issue.StringId, sample.Value<string>("issueId")));
            Assert.True(samples[0].Value<bool>("permitted"));
            Assert.False(samples[1].Value<bool>("permitted"));
            Assert.True(giver.Issue.IsOngoingWithoutQuest);
            Assert.Null(giver.Issue.IssueQuest);
        });
    }

    [Theory]
    [InlineData("charm")]
    [InlineData("mercy")]
    [InlineData("female")]
    [InlineData("persona_curt")]
    [InlineData("persona_ironic")]
    [InlineData("in_bloom")]
    [InlineData("young_and_respectful")]
    [InlineData("good_natured")]
    [InlineData("tribute")]
    public void SocialParameterWritesReplicateAndRestoreWithoutAcceptingStaleOrClientWrites(string parameter)
    {
        var heroId = environment.CreateRegisteredObject<Hero>();
        var command = new HeroDeveloperCommands.HeroSocialParameterCoopCommand();
        var args = new CoopCommandArgsFactory();
        int ReadValue()
        {
            var result = command.ProcessCommand(args.FromValues(new[] { heroId, parameter }));
            Assert.True(result.Succeeded, result.Output);
            return JObject.Parse(result.Output).Value<int>("value");
        }
        var previous = 0;
        var changed = 0;
        void Observe(EnvironmentInstance instance, string phase)
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            MBObjectBase key = parameter switch
            {
                "charm" => DefaultSkills.Charm,
                "mercy" => DefaultTraits.Mercy,
                "persona_curt" => DefaultTraits.PersonaCurt,
                "persona_ironic" => DefaultTraits.PersonaIronic,
                "in_bloom" => DefaultPerks.Charm.InBloom,
                "young_and_respectful" => DefaultPerks.Charm.YoungAndRespectful,
                "good_natured" => DefaultPerks.Charm.GoodNatured,
                "tribute" => DefaultPerks.Charm.Tribute,
                _ => null,
            };
            var heroRegistered = instance.ObjectManager.TryGetHandle(hero, out var heroHandle);
            var keyRegistered = instance.ObjectManager.TryGetHandle(key, out var keyHandle);
            JArray Messages(System.Collections.Generic.IEnumerable<IMessage> messages)
            {
                var rows = new JArray();
                foreach (var item in messages.Select((message, index) => new { message, index }))
                {
                    var type = item.message.GetType();
                    if (!type.Name.StartsWith("Hero__heroTraits_Set") &&
                        !type.Name.StartsWith("Hero__heroSkills_Set") &&
                        !type.Name.StartsWith("Hero__heroPerks_Set")) continue;
                    var row = new JObject { ["index"] = item.index, ["type"] = type.FullName };
                    // Generated message types exist only after the runtime AutoSync build.
                    foreach (var name in new[] { "InstanceId", "ValueId", "PropertyValue" })
                    {
                        var property = type.GetProperty(name);
                        if (property != null) row[name] = JToken.FromObject(property.GetValue(item.message));
                    }
                    foreach (var name in new[] { "Instance", "Value" })
                    {
                        var property = type.GetProperty(name);
                        if (property == null) continue;
                        var value = property.GetValue(item.message);
                        row[name + "Registered"] = instance.ObjectManager.TryGetHandle(value, out var handle);
                        row[name + "Handle"] = handle;
                        row[name + "StringId"] = (value as MBObjectBase)?.StringId;
                    }
                    rows.Add(row);
                }
                return rows;
            }
            output.WriteLine(new JObject
            {
                ["diagnostic"] = "social-parameter", ["phase"] = phase, ["parameter"] = parameter,
                ["role"] = ReferenceEquals(instance, Server) ? "server" : "client-" + Array.IndexOf(environment.Clients.ToArray(), instance),
                ["heroId"] = heroId, ["heroRegistered"] = heroRegistered, ["heroHandle"] = heroHandle,
                ["keyType"] = key?.GetType().FullName, ["keyId"] = key?.StringId,
                ["keyRegistered"] = keyRegistered, ["keyHandle"] = keyHandle,
                ["value"] = ReadValue(), ["pendingApplies"] = instance.PendingGameThreadActionCount,
                ["sent"] = Messages(instance.NetworkSentMessages), ["published"] = Messages(instance.InternalMessages),
            }.ToString(Newtonsoft.Json.Formatting.None));
        }
        Server.Call(() =>
        {
            previous = ReadValue();
            Observe(Server, "before-write");
            changed = previous == 0 ? 1 : 0;
            Assert.False(command.ProcessCommand(args.FromValues(new[] { heroId, parameter, changed.ToString() })).Succeeded);
            Assert.False(command.ProcessCommand(args.FromValues(new[] { heroId, parameter, int.MaxValue.ToString(), previous.ToString() })).Succeeded);
            var result = command.ProcessCommand(args.FromValues(new[] { heroId, parameter, changed.ToString(), previous.ToString() }));
            Assert.True(result.Succeeded, result.Output);
            Assert.Equal(previous, JObject.Parse(result.Output).Value<int>("before"));
            Assert.Equal(changed, ReadValue());
            Assert.False(command.ProcessCommand(args.FromValues(new[] { heroId, parameter, previous.ToString(), previous.ToString() })).Succeeded);
            Assert.Equal(changed, ReadValue());
            Observe(Server, "after-write");
        });
        foreach (var client in environment.Clients)
            client.Call(() => Observe(client, "before-pump"));
        foreach (var client in environment.Clients)
        {
            client.PumpGameThread();
            client.Call(() =>
            {
                Observe(client, "after-pump");
                Assert.Equal(changed, ReadValue());
                Assert.False(command.ProcessCommand(args.FromValues(new[] { heroId, parameter, previous.ToString(), changed.ToString() })).Succeeded);
                Assert.Equal(changed, ReadValue());
            });
        }
        Server.Call(() => Assert.True(command.ProcessCommand(args.FromValues(
            new[] { heroId, parameter, previous.ToString(), changed.ToString() })).Succeeded));
        foreach (var client in environment.Clients)
        {
            client.PumpGameThread();
            client.Call(() => Assert.Equal(previous, ReadValue()));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EligibilityCannotBorrowAnotherPlayersMissingHeroOrParty(bool missingHero)
    {
        var created = CreateIssue();
        var heroId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var controller = "missing-object-player";
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(controller,
                missingHero ? "missing-hero" : heroId, missingHero ? partyId : "missing-party", "", "")));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(created.GiverId, out var giver));
            var issue = giver.Issue;
            var previousHero = ResolvedMainHeroContext.ResolvedMainHero;
            var previousParty = Campaign.Current.MainParty;

            var result = new ConquestQuestCommands.Eligibility().ProcessCommand(
                new CoopCommandArgsFactory().FromValues(new[] { created.GiverId, controller }));

            Assert.False(result.Succeeded);
            Assert.Same(issue, giver.Issue);
            Assert.True(issue.IsOngoingWithoutQuest);
            Assert.Null(issue.IssueQuest);
            Assert.Same(previousHero, ResolvedMainHeroContext.ResolvedMainHero);
            Assert.Same(previousParty, Campaign.Current.MainParty);
        });
    }

    [Fact]
    public void JournalCannotOpenOnTheDedicatedAuthority()
    {
        Server.Call(() =>
        {
            var result = new ConquestQuestCommands.Journal().ProcessCommand(
                new CoopCommandArgsFactory().FromValues(new[] { "open" }));
            Assert.False(result.Succeeded);
        });
    }

    [Fact]
    public void ClientAcceptanceRetainsTheObservedGenerationAndCannotRunOnTheServer()
    {
        var created = CreateIssue();
        var command = new ConquestQuestCommands.RequestAccept();
        var args = new CoopCommandArgsFactory();
        Server.Call(() => Assert.False(command.ProcessCommand(args.FromValues(new[] { created.GiverId, "73" })).Succeeded));
        var client = environment.Clients.First();
        client.Call(() =>
        {
            Assert.False(command.ProcessCommand(args.FromValues(new[] { created.GiverId, "-1" })).Succeeded);
            Assert.False(command.ProcessCommand(args.FromValues(new[] { created.GiverId, "invalid" })).Succeeded);
            Assert.Empty(client.NetworkSentMessages.GetMessages<RequestQuestTypeAcceptQuest>());
            Assert.True(command.ProcessCommand(args.FromValues(new[] { created.GiverId, "73" })).Succeeded);
        });
        var request = Assert.Single(client.NetworkSentMessages.GetMessages<RequestQuestTypeAcceptQuest>());
        Assert.Equal(created.GiverId, request.OwnerId);
        Assert.Equal(73, request.Generation);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(created.GiverId, out var giver));
            Assert.Null(giver.Issue.IssueQuest);
        });
    }
#endif

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NpcDeathRejectsMissingActorObjectsBeforeAddingADeathMark(bool missingHero)
    {
        var npcId = environment.CreateRegisteredObject<Hero>();
        var actorId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var command = new HeroDebugCommand.HeroKillNpcCoopCommand();
        var args = new CoopCommandArgsFactory().FromValues(new[] { npcId, "old_age", "death-actor" });
        foreach (var client in environment.Clients)
            client.Call(() => Assert.False(command.ProcessCommand(args).Succeeded));
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player("death-actor",
                missingHero ? "missing-hero" : actorId, missingHero ? partyId : "missing-party", "", "")));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(npcId, out var npc));
            Assert.True(npc.IsAlive);
            var previousDeathMark = npc.DeathMark;
            var result = command.ProcessCommand(args);
            Assert.False(result.Succeeded);
            Assert.Contains("registered hero and party", result.Output);
            Assert.True(npc.IsAlive);
            Assert.Equal(previousDeathMark, npc.DeathMark);
        });
    }

    [Fact]
    public void NpcAndPlayerDeathCommandsCannotCrossTheirTargetRolesOrAcceptAnUnknownKiller()
    {
        var npcId = environment.CreateRegisteredObject<Hero>();
        var actorId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player("death-actor", actorId, partyId, "", "")));
            var args = new CoopCommandArgsFactory();
            Assert.False(new HeroDebugCommand.HeroKillPlayerCoopCommand().ProcessCommand(
                args.FromValues(new[] { npcId, "old_age" })).Succeeded);
            var command = new HeroDebugCommand.HeroKillNpcCoopCommand();
            Assert.False(command.ProcessCommand(args.FromValues(new[] { actorId, "old_age", "death-actor" })).Succeeded);
            Assert.False(command.ProcessCommand(args.FromValues(new[] { npcId, "execution", "death-actor" })).Succeeded);
            Assert.False(command.ProcessCommand(args.FromValues(new[] { npcId, "battle", "death-actor", "missing-killer" })).Succeeded);
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(npcId, out var npc));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(actorId, out var actor));
            Assert.True(npc.IsAlive);
            Assert.True(actor.IsAlive);
            Assert.Equal(KillCharacterAction.KillCharacterActionDetail.None, npc.DeathMark);
            Assert.Equal(KillCharacterAction.KillCharacterActionDetail.None, actor.DeathMark);
        });
    }

    [Fact]
    public void NpcExecutionRejectsAnotherPlayerAsKillerBeforeAddingADeathMark()
    {
        var npcId = environment.CreateRegisteredObject<Hero>();
        var actorId = environment.CreateRegisteredObject<Hero>();
        var actorPartyId = environment.CreateRegisteredObject<MobileParty>();
        var killerId = environment.CreateRegisteredObject<Hero>();
        var killerPartyId = environment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var players = Server.Resolve<IPlayerManager>();
            Assert.True(players.AddPlayer(new Player("death-actor", actorId, actorPartyId, "", "")));
            Assert.True(players.AddPlayer(new Player("other-executor", killerId, killerPartyId, "", "")));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(npcId, out var npc));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(actorId, out var actor));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(killerId, out var killer));
            var previousDeathMark = npc.DeathMark;
            var previousHero = ResolvedMainHeroContext.ResolvedMainHero;
            var previousParty = Campaign.Current.MainParty;
            var killed = 0;
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, (_, _, _, _) => killed++);

            var result = new HeroDebugCommand.HeroKillNpcCoopCommand().ProcessCommand(
                new CoopCommandArgsFactory().FromValues(new[] { npcId, "execution", "death-actor", killerId }));

            Assert.False(result.Succeeded);
            Assert.Contains("must match the acting controller", result.Output);
            Assert.True(npc.IsAlive);
            Assert.True(actor.IsAlive);
            Assert.True(killer.IsAlive);
            Assert.Equal(previousDeathMark, npc.DeathMark);
            Assert.Equal(0, killed);
            Assert.Same(previousHero, ResolvedMainHeroContext.ResolvedMainHero);
            Assert.Same(previousParty, Campaign.Current.MainParty);
        });
    }

    [Theory]
    [InlineData("old_age", KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge)]
    [InlineData("battle", KillCharacterAction.KillCharacterActionDetail.DiedInBattle)]
    [InlineData("execution", KillCharacterAction.KillCharacterActionDetail.Executed)]
    public void NpcDeathRunsNativeCallbacksInTheSelectedPlayerContextAndRestoresIt(
        string detail, KillCharacterAction.KillCharacterActionDetail expected)
    {
        var npcId = environment.CreateRegisteredObject<Hero>();
        var actorId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player("death-actor", actorId, partyId, "", "")));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(npcId, out var npc));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(actorId, out var actor));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            using (new AllowedThread()) npc.Clan = null;
            var previousHero = ResolvedMainHeroContext.ResolvedMainHero;
            var previousParty = Campaign.Current.MainParty;
            var killed = 0;
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, (victim, killer, actionDetail, _) =>
            {
                Assert.Same(npc, victim);
                Assert.Same(actor, killer);
                Assert.Same(actor, Hero.MainHero);
                Assert.Same(party, MobileParty.MainParty);
                Assert.Equal(expected, actionDetail);
                killed++;
            });
            var result = new HeroDebugCommand.HeroKillNpcCoopCommand().ProcessCommand(
                new CoopCommandArgsFactory().FromValues(new[] { npcId, detail, "death-actor", actorId }));
            Assert.True(result.Succeeded);
            Assert.Equal(1, killed);
            Assert.False(npc.IsAlive);
            Assert.True(actor.IsAlive);
            Assert.Same(previousHero, ResolvedMainHeroContext.ResolvedMainHero);
            Assert.Same(previousParty, Campaign.Current.MainParty);
        }, new[] { AccessTools.Method(typeof(KillCharacterAction), "CreateObituary") });
    }

    [Fact]
    public void RoutingSendsOnlyTheValidatedMapEventToTheMissionAuthority()
    {
        var id = environment.CreateRegisteredObject<MapEvent>();
        var command = new MapEventDebugCommands.RouteBattleEnemiesCoopCommand();
        var registry = new CoopCommandRegistry(new[] { command }, Common.Logging.LogManager.GetLogger<TheConquestOfSettlementIssueTests>());
        var args = new CoopCommandArgsFactory();
        foreach (var client in environment.Clients)
            client.Call(() => Assert.False(command.ProcessCommand(args.FromValues(new[] { id, "0" })).Succeeded));
        Server.Call(() =>
        {
            Assert.False(command.ProcessCommand(args.FromValues(new[] { id, "-1" })).Succeeded);
            Assert.False(command.ProcessCommand(args.FromValues(new[] { id, "0", "both" })).Succeeded);
            Assert.False(command.ProcessCommand(args.FromValues(new[] { "missing-event", "0" })).Succeeded);
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkRouteBattleEnemies>());
            Assert.True(registry.ProcessCommand("coop.debug.map_event.route_enemies", args.FromValues(new[] { id, "3" })).Succeeded);
        });
        var request = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkRouteBattleEnemies>());
        Assert.Equal(id, request.MapEventId);
        Assert.Equal(3, request.EnemiesToLeaveFighting);
        Assert.Null(request.Side);
    }

    [Theory]
    [InlineData("attacker", BattleSideEnum.Attacker)]
    [InlineData("defender", BattleSideEnum.Defender)]
    public void RoutingCanSelectEitherSideWithoutAssigningABattleResult(string side, BattleSideEnum expected)
    {
        var id = environment.CreateRegisteredObject<MapEvent>();
        var command = new MapEventDebugCommands.RouteBattleEnemiesCoopCommand();
        var args = new CoopCommandArgsFactory().FromValues(new[] { id, "0", side });
        foreach (var client in environment.Clients)
            client.Call(() => Assert.False(command.ProcessCommand(args).Succeeded));
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(id, out var mapEvent));
            var winner = mapEvent.WinningSide;
            Assert.True(command.ProcessCommand(args).Succeeded);
            Assert.Equal(winner, mapEvent.WinningSide);
            Assert.False(mapEvent.IsFinalized);
        });
        var request = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkRouteBattleEnemies>());
        Assert.Equal(id, request.MapEventId);
        Assert.Equal(expected, request.Side);
        Assert.Equal(0, request.EnemiesToLeaveFighting);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WarCommandCannotBorrowAnotherPlayersMissingHeroOrParty(bool missingHero)
    {
        var faction1Id = environment.CreateRegisteredObject<Kingdom>();
        var faction2Id = environment.CreateRegisteredObject<Kingdom>();
        var heroId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var controller = "missing-war-actor";
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(controller,
                missingHero ? "missing-hero" : heroId, missingHero ? partyId : "missing-party", "", "")));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(faction1Id, out var faction1));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(faction2Id, out var faction2));
            var previousHero = ResolvedMainHeroContext.ResolvedMainHero;
            var previousParty = Campaign.Current.MainParty;

            var result = new KingdomDebugCommand.KingdomDeclareWarCoopCommand().ProcessCommand(
                new CoopCommandArgsFactory().FromValues(new[] { faction1Id, faction2Id, "hostility", controller }));

            Assert.False(result.Succeeded);
            Assert.Contains("registered hero and party", result.Output);
            Assert.False(FactionManager.IsAtWarAgainstFaction(faction1, faction2));
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>());
            Assert.Same(previousHero, ResolvedMainHeroContext.ResolvedMainHero);
            Assert.Same(previousParty, Campaign.Current.MainParty);
        });
    }

    private NetworkConquestIssueCreated CreateIssue()
    {
        var giverId = environment.CreateRegisteredObject<Hero>();
        var targetId = environment.CreateRegisteredObject<Settlement>();
        foreach (var instance in new[] { Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                    if (instance != Server) Campaign.Current.IssueManager._nextIssueUniqueIndex = 200;
                }
            });
        }
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(targetId, out var target));
            var potential = new PotentialIssueData((in PotentialIssueData _, Hero owner) => new Issue(owner, target),
                typeof(Issue), IssueBase.IssueFrequency.VeryCommon);
            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, giver));
        });
        foreach (var client in environment.Clients)
            client.PumpGameThread();
        return Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkConquestIssueCreated>());
    }

    [Fact]
    public void CreationKeepsTheServerTargetDeadlineAndIdentityAcrossDifferentClientCounters()
    {
        var created = CreateIssue();
        Assert.StartsWith("coop_conquest_issue_", created.IssueId);
        foreach (var instance in new[] { Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(created.GiverId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(created.TargetId, out var target));
                var issue = Assert.IsType<Issue>(giver.Issue);
                Assert.Same(target, issue._targetSettlement);
                Assert.Equal(created.IssueId, issue.StringId);
                Assert.Equal(created.DueTime, issue.IssueDueTime);
                Assert.True(instance.Resolve<IIssueGenerationRegistry>().TryGetGeneration(giver, out var generation));
                Assert.Equal(created.Generation, generation);
            });
        }
    }

    [Fact]
    public void DelayedCreationCannotReplaceTheCurrentIssue()
    {
        var created = CreateIssue();
        var differentTargetId = environment.CreateRegisteredObject<Settlement>();
        foreach (var client in environment.Clients)
        {
            client.SimulateMessage(this, new NetworkConquestIssueCreated(created.GiverId, differentTargetId,
                created.Generation - 1, CampaignTime.Never, "old-issue"));
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(created.GiverId, out var giver));
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(created.TargetId, out var target));
                var issue = Assert.IsType<Issue>(giver.Issue);
                Assert.Same(target, issue._targetSettlement);
                Assert.Equal(created.IssueId, issue.StringId);
                Assert.Equal(created.DueTime, issue.IssueDueTime);
            });
        }
    }

    [Theory]
    [InlineData("owner", true)]
    [InlineData("other", false)]
    [InlineData("unknown", false)]
    public void HostilityUsesTheInitiatorCapturedBeforeQuestOwnerSubstitution(string initiator, bool causedByOwner)
    {
        var ownerId = environment.CreateRegisteredObject<Hero>();
        var otherId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(otherId, out var other));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            var wasApplying = ConquestQuestEventPatches.IsApplying;
            var previousTrigger = ConquestQuestEventPatches.TriggeringHero;
            using (new MainHeroSubstitutionScope(owner, party))
            {
                try
                {
                    ConquestQuestEventPatches.IsApplying = true;
                    ConquestQuestEventPatches.TriggeringHero = initiator == "owner" ? owner : initiator == "other" ? other : null;
                    Assert.Equal(causedByOwner, DiplomacyHelper.IsWarCausedByPlayer(null, null,
                        DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility));
                }
                finally
                {
                    ConquestQuestEventPatches.TriggeringHero = previousTrigger;
                    ConquestQuestEventPatches.IsApplying = wasApplying;
                }
            }
        });
    }

    [Fact]
    public void PlayerRemovalCancelsOnlyItsQuestEvenAfterThePlayerObjectsAreGone()
    {
        var created = CreateIssue();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(created.GiverId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(created.TargetId, out var target));
            var quest = new Quest(created.IssueId + "_quest", giver, target, CampaignTime.DaysFromNow(60), 20000);
            var unrelated = ObjectHelper.SkipConstructor<Quest>();
            var ownership = Server.Resolve<IIssueOwnershipRegistry>();
            using (new AllowedThread())
            {
                unrelated.StringId = "another_players_quest";
                giver.Issue.IssueQuest = quest;
                giver.Issue.IsTriedToSolveBefore = true;
                giver.Issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
                Campaign.Current.QuestManager.OnQuestStarted(quest);
                Campaign.Current.QuestManager.OnQuestStarted(unrelated);
            }
            ownership.SetOwner(giver, "removed-player");
            ownership.SetQuestOwner(quest.StringId, "removed-player");
            ownership.SetQuestOwner(unrelated.StringId, "another-player");
            Assert.Equal(created.IssueId + "_quest", quest.StringId);
            Assert.Equal("another_players_quest", unrelated.StringId);
            Assert.True(quest.IsOngoing);
            Assert.True(unrelated.IsOngoing);
            Assert.Contains(quest, Campaign.Current.QuestManager.Quests);
            Assert.Contains(unrelated, Campaign.Current.QuestManager.Quests);
            Assert.True(ownership.TryGetQuestOwner(quest.StringId, out var questOwner));
            Assert.Equal("removed-player", questOwner);
            Assert.True(ownership.TryGetQuestOwner(unrelated.StringId, out var unrelatedOwner));
            Assert.Equal("another-player", unrelatedOwner);

            Server.Resolve<IConquestQuest>().CancelForPlayerRemoval("removed-player");

            Assert.True(quest.IsFinalized);
            Assert.Null(giver.Issue);
            Assert.DoesNotContain(quest, Campaign.Current.QuestManager.Quests);
            Assert.Contains(unrelated, Campaign.Current.QuestManager.Quests);
            Assert.True(unrelated.IsOngoing);
            Assert.True(ownership.TryGetQuestOwner(unrelated.StringId, out var retainedOwner));
            Assert.Equal("another-player", retainedOwner);
            Assert.False(ownership.TryGetOwnerControllerId(giver, out _));
            Assert.Null(ConquestQuest.CancellingRemovedPlayerQuest);
            Assert.True(ownership.TryGetQuestOwner(quest.StringId, out var historicalOwner));
            Assert.Equal("removed-player", historicalOwner);
            Campaign.Current.QuestManager.OnQuestFinalized(unrelated);
        });
    }
}
