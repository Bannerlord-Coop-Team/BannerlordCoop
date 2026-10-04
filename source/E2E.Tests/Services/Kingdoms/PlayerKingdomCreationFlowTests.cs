using Common;
using Common.Commands;
using Common.Network;
using Common.Messaging;
using Common.Logging;
using Common.Util;
using Coop.Core.Client.Services.Kingdoms.Handlers;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.Kingdoms.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Core.Server.Services.Stances.Messages;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.MockEngine;
using E2E.Tests.Util;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Clans.Patches;
using GameInterface.Services.Entity;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameDebug.Commands;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Commands;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms.Extentions;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Services.Locations.Conversations;
using GameInterface.Services.Locations.Conversations.Patches;
using GameInterface.Services.Locations.Messages.Conversation;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Stances.Messages;
using GameInterface.Services.UI.Notifications.Messages;
using GameInterface.Services.Villages.Interfaces;
using HarmonyLib;
using SandBox.Conversation.MissionLogics;
using SandBox.ViewModelCollection.Map.Tracker;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Policies;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Kingdoms;

public class PlayerKingdomCreationFlowTests : IDisposable
{
    private const string ControllerId = "Player";
    private const string SecondControllerId = "Player2";
    private const string KingdomName = "Real Kingdom";

    private E2ETestEnvironment TestEnvironment { get; }
    private readonly ITestOutputHelper output;
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;
    private static IKingdomDecisionVoteManager GetVoteManager(EnvironmentInstance instance) =>
        instance.Resolve<IKingdomDecisionVoteManager>();
    private static KingdomDecisionVoteManager GetConcreteVoteManager(EnvironmentInstance instance) =>
        instance.Resolve<KingdomDecisionVoteManager>();

    public PlayerKingdomCreationFlowTests(ITestOutputHelper output)
    {
        this.output = output;
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void DiplomacyDebugActionsRequireAnActorAndDispatchInsideThatPlayersScope()
    {
        var player = CreateSyncedPlayerContext();
        var enemy = CreateSyncedPlayerContext("DiplomacyEnemy", _ => false);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var enemyId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        ConfigureClanInKingdom(player.ClanId, kingdomId);
        ConfigureClanInKingdom(enemy.ClanId, enemyId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(enemyId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(enemyId, out var enemyKingdom));
            var previousHero = ResolvedMainHeroContext.ResolvedMainHero;
            var previousParty = Campaign.Current.MainParty;
            var args = new CoopCommandArgsFactory();
            var war = new KingdomDebugCommand.KingdomDeclareWarCoopCommand();
            Assert.False(war.ProcessCommand(args.FromValues(new[] { kingdomId, enemyId })).Succeeded);
            Assert.False(war.ProcessCommand(args.FromValues(new[] { kingdomId, enemyId, "hostility", "none" })).Succeeded);
            Assert.False(kingdom.IsAtWarWith(enemyKingdom));
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>());

            var wars = 0;
            var peaces = 0;
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, (first, second, detail) =>
            {
                Assert.Same(hero, Hero.MainHero);
                Assert.Equal(DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility, detail);
                wars++;
            });
            CampaignEvents.MakePeace.AddNonSerializedListener(this, (first, second, detail) =>
            {
                Assert.Same(hero, Hero.MainHero);
                peaces++;
            });
            try
            {
                var result = war.ProcessCommand(args.FromValues(new[] { kingdomId, enemyId, "hostility", ControllerId }));
                Assert.True(result.Succeeded, result.Output);
                Assert.Equal(1, wars);
                Assert.True(kingdom.IsAtWarWith(enemyKingdom));
                Assert.Same(previousHero, ResolvedMainHeroContext.ResolvedMainHero);
                Assert.Same(previousParty, Campaign.Current.MainParty);

                var peace = new KingdomDebugCommand.KingdomMakePeaceCoopCommand();
                Assert.False(peace.ProcessCommand(args.FromValues(new[] { kingdomId, enemyId })).Succeeded);
                Assert.True(kingdom.IsAtWarWith(enemyKingdom));
                result = peace.ProcessCommand(args.FromValues(new[] { kingdomId, enemyId, ControllerId }));
                Assert.True(result.Succeeded, result.Output);
                Assert.Equal(1, peaces);
                Assert.False(kingdom.IsAtWarWith(enemyKingdom));
                Assert.Same(previousHero, ResolvedMainHeroContext.ResolvedMainHero);
                Assert.Same(previousParty, Campaign.Current.MainParty);
            }
            finally
            {
                CampaignEvents.WarDeclared.ClearListeners(this);
                CampaignEvents.MakePeace.ClearListeners(this);
            }
        }, new[] { AccessTools.Method(typeof(DefaultAllianceModel), nameof(DefaultAllianceModel.GetCallToWarCost)) });
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>());
        foreach (var client in Clients)
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
                Assert.True(client.ObjectManager.TryGetObject<Kingdom>(enemyId, out var enemyKingdom));
                Assert.False(kingdom.IsAtWarWith(enemyKingdom));
            });
    }

    [Fact]
    public void RebellionDebugActionRequiresAnActorAndCompletesClanMembershipChanges()
    {
        var player = CreateSyncedPlayerContext();
        var ruler = CreateSyncedPlayerContext("RebellionRuler", _ => false);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        ConfigureClanInKingdom(ruler.ClanId, kingdomId);
        ConfigureClanInKingdom(player.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        void Observe(EnvironmentInstance instance, string phase)
        {
            Assert.True(instance.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));
            Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var clanRegistered = instance.ObjectManager.TryGetHandle(clan, out var clanHandle);
            var kingdomRegistered = instance.ObjectManager.TryGetHandle(kingdom, out var kingdomHandle);
            var setter = AccessTools.PropertySetter(typeof(Clan), nameof(Clan.Kingdom));
            var internalSetter = AccessTools.Method(typeof(Clan), nameof(Clan.SetKingdomInternal));
            output.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                diagnostic = "rebellion-membership", phase,
                role = ReferenceEquals(instance, Server) ? "server" : "client-" + Array.IndexOf(Clients.ToArray(), instance),
                player.ClanId, clanRegistered, clanHandle, kingdomId, kingdomRegistered, kingdomHandle,
                clanKingdom = clan.Kingdom?.StringId, backingKingdom = clan._kingdom?.StringId,
                kingdomClans = kingdom._clans?.Select(member => member?.StringId).ToArray(),
                pendingApplies = instance.PendingGameThreadActionCount,
                localSets = instance.InternalMessages.GetMessages<SetClanKingdom>()
                    .Select(message => new { clan = message.Clan?.StringId, kingdom = message.Kingdom?.StringId }).ToArray(),
                sentSets = instance.NetworkSentMessages.GetMessages<NetworkSetClanKingdom>().ToArray(),
                receivedSets = instance.InternalMessages.GetMessages<NetworkSetClanKingdom>().ToArray(),
                sentChanges = instance.NetworkSentMessages.GetMessages<NetworkOnClanChangedKingdom>().ToArray(),
                receivedChanges = instance.InternalMessages.GetMessages<NetworkOnClanChangedKingdom>().ToArray(),
                membershipMessageOrder = instance.InternalMessages.Select((message, index) => new { message, index })
                    .Where(item => item.message is SetClanKingdom || item.message is NetworkSetClanKingdom ||
                        item.message is NetworkOnClanChangedKingdom || item.message.GetType().Name.StartsWith("Kingdom__clans_"))
                    .Select(item => new { item.index, type = item.message.GetType().FullName }).ToArray(),
                setterPrefixes = Harmony.GetPatchInfo(setter)?.Prefixes
                    .Select(patch => patch.PatchMethod.DeclaringType.FullName + "." + patch.PatchMethod.Name).ToArray(),
                internalSetterPrefixes = Harmony.GetPatchInfo(internalSetter)?.Prefixes
                    .Select(patch => patch.PatchMethod.DeclaringType.FullName + "." + patch.PatchMethod.Name).ToArray(),
            }));
        }
        foreach (var instance in new[] { Server }.Concat(Clients))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
                using (new AllowedThread())
                {
                    kingdom.Name = new TextObject("Rebellion kingdom");
                }
                Observe(instance, "before-command");
            });

        var notifiedMemberships = new Dictionary<EnvironmentInstance, List<Kingdom>>();
        foreach (var client in Clients)
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));
                var memberships = new List<Kingdom>();
                notifiedMemberships.Add(client, memberships);
                CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this,
                    (changed, oldKingdom, newKingdom, detail, show) =>
                    {
                        if (changed == clan) memberships.Add(clan.Kingdom);
                    });
            });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var previousHero = ResolvedMainHeroContext.ResolvedMainHero;
            var previousParty = Campaign.Current.MainParty;
            var command = new ClanDebugCommands.ClanLeaveKingdomCoopCommand();
            var args = new CoopCommandArgsFactory();
            var missingActor = command.ProcessCommand(args.FromValues(new[] { player.ClanId, "rebellion" }));
            Assert.False(missingActor.Succeeded);
            Assert.Contains("An acting player's registered hero and party are required.", missingActor.Output);
            Assert.Same(kingdom, clan.Kingdom);
            Assert.Contains(clan, kingdom.Clans);
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>());
            var changes = 0;
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, (changed, oldKingdom, newKingdom, detail, show) =>
            {
                Assert.Same(hero, Hero.MainHero);
                Assert.Same(clan, changed);
                Assert.Equal(ChangeKingdomAction.ChangeKingdomActionDetail.LeaveWithRebellion, detail);
                changes++;
            });
            try
            {
                var result = command.ProcessCommand(args.FromValues(new[] { player.ClanId, "rebellion", ControllerId }));
                Observe(Server, "after-command");
                Assert.True(result.Succeeded, result.Output);
                Assert.Equal(1, changes);
                Assert.True(clan.IsAtWarWith(kingdom));
                Assert.Same(previousHero, ResolvedMainHeroContext.ResolvedMainHero);
                Assert.Same(previousParty, Campaign.Current.MainParty);
            }
            finally
            {
                CampaignEvents.OnClanChangedKingdomEvent.ClearListeners(this);
            }
        }, new[] { AccessTools.Method(typeof(DefaultAllianceModel), nameof(DefaultAllianceModel.GetCallToWarCost)) });
        Assert.Contains(Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>(),
            message => message.Detail == (int)DeclareWarAction.DeclareWarDetail.CausedByRebellion);
        foreach (var instance in new[] { Server }.Concat(Clients))
            instance.Call(() => Observe(instance, "before-pump"));
        foreach (var client in Clients)
            client.PumpGameThread();
        foreach (var instance in new[] { Server }.Concat(Clients))
            instance.Call(() => Observe(instance, "after-pump"));
        foreach (var client in Clients)
            client.Call(() =>
            {
                CampaignEvents.OnClanChangedKingdomEvent.ClearListeners(this);
                Assert.Null(Assert.Single(notifiedMemberships[client]));
            });
        foreach (var instance in new[] { Server }.Concat(Clients))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));
                Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
                Assert.Null(clan.Kingdom);
                Assert.DoesNotContain(clan, kingdom.Clans);
            });
    }

    [Fact]
    public void RepeatedKingdomMembershipDoesNotResetClientInfluence()
    {
        var player = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        ConfigureClanInKingdom(player.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        const float influence = 123f;
        foreach (var client in Clients)
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));
                using (new AllowedThread())
                {
                    clan._influence = influence;
                }
            });

        Server.Call(() => Server.Resolve<INetwork>().SendAll(new NetworkSetClanKingdom(player.ClanId, kingdomId)));
        foreach (var client in Clients)
        {
            client.PumpGameThread();
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));
                Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
                Assert.Same(kingdom, clan.Kingdom);
                Assert.Equal(influence, clan.Influence);
                Assert.Same(clan, Assert.Single(kingdom.Clans));
                Assert.Contains(client.InternalMessages.GetMessages<NetworkSetClanKingdom>(),
                    message => message.ClanId == player.ClanId && message.KingdomId == kingdomId);
            });
        }
    }

    [Fact]
    public void ClientGovernorFinalization_CreatesPlayerKingdomOnServerAndSyncsClients()
    {
        var player = CreateSyncedPlayerContext();
        var fiefId = CreateSyncedClanFief(player.ClanId);
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        client.Call(() =>
        {
            var behavior = ObjectHelper.SkipConstructor<GovernorCampaignBehavior>();
            Assert.True(client.ObjectManager.TryGetObject<CultureObject>(player.CultureId, out var culture));

            AccessTools.Field(typeof(GovernorCampaignBehavior), "_kingdomCreationChosenName")
                .SetValue(behavior, new TextObject(KingdomName));
            AccessTools.Field(typeof(GovernorCampaignBehavior), "_kingdomCreationChosenCulture")
                .SetValue(behavior, culture);

            bool runOriginal = GovernorKingdomCreationPatches.FinalizationPrefix(behavior);

            Assert.False(runOriginal);
        });

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestCreateKingdom>());
        Assert.Equal(ControllerId, request.ControllerId);
        Assert.Equal(KingdomName, request.KingdomName);
        Assert.False(string.IsNullOrWhiteSpace(request.CultureId));

        var created = Assert.Single(Server.InternalMessages.GetMessages<PlayerKingdomCreated>());
        Assert.Equal(ControllerId, created.ControllerId);
        Assert.Equal(KingdomName, created.KingdomName);
        Assert.Equal(player.ClanId, created.ClanId);
        Assert.Equal(request.CultureId, created.CultureId);
        Assert.False(string.IsNullOrWhiteSpace(created.KingdomId));

        AssertKingdomCreatedOnServer(created.KingdomId, player.ClanId, created.CultureId, fiefId);
        foreach (var environmentClient in Clients)
        {
            AssertKingdomSyncedToClient(environmentClient, created.KingdomId, player.ClanId, created.CultureId, fiefId);
            Assert.Contains(
                environmentClient.InternalMessages.GetMessages<PlayerKingdomCreated>(),
                message => message.KingdomId == created.KingdomId
                           && message.KingdomName == KingdomName
                           && message.ClanId == player.ClanId);
        }

        var notification = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPlayerKingdomCreated>());
        Assert.Equal(created.ControllerId, notification.ControllerId);
        Assert.Equal(created.KingdomId, notification.KingdomId);
        Assert.Equal(created.KingdomName, notification.KingdomName);
        Assert.Equal(created.ClanId, notification.ClanId);
        Assert.Equal(created.CultureId, notification.CultureId);

        Server.PumpGameThread();
    }

    [Fact]
    public void CreateKingdomDebugCommand_OnServer_CreatesKingdomAndSyncsClients()
    {
        var player = CreateSyncedPlayerContext();
        var fiefId = CreateSyncedClanFief(player.ClanId);
        SetClanCultureEverywhere(player.ClanId, player.CultureId);

        string output = null;
        Server.Call(() =>
        {
            output = ExecuteCommand(
                new KingdomDebugCommand.KingdomCreateCoopCommand(),
                player.HeroId,
                KingdomName);
        });

        Assert.StartsWith("Created kingdom", output);

        var created = Assert.Single(Server.InternalMessages.GetMessages<PlayerKingdomCreated>());
        Assert.Equal(ControllerId, created.ControllerId);
        Assert.Equal(KingdomName, created.KingdomName);
        Assert.Equal(player.ClanId, created.ClanId);
        Assert.Equal(player.CultureId, created.CultureId);
        Assert.False(string.IsNullOrWhiteSpace(created.KingdomId));

        AssertKingdomCreatedOnServer(created.KingdomId, player.ClanId, player.CultureId, fiefId);
        foreach (var environmentClient in Clients)
        {
            AssertKingdomSyncedToClient(environmentClient, created.KingdomId, player.ClanId, player.CultureId, fiefId);
        }

        var notification = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPlayerKingdomCreated>());
        Assert.Equal(created.KingdomId, notification.KingdomId);
        Assert.Equal(KingdomName, notification.KingdomName);
        Assert.Equal(player.ClanId, notification.ClanId);

        Server.PumpGameThread();
    }

    [Fact]
    public void CreateKingdomDebugCommand_OnClient_IsRejected()
    {
        var player = CreateSyncedPlayerContext();
        var client = Clients.First();

        string output = null;
        client.Call(() =>
        {
            output = ExecuteCommand(
                new KingdomDebugCommand.KingdomCreateCoopCommand(),
                player.HeroId,
                KingdomName);
        });

        Assert.Equal("This command can only be run on the server.", output);
        Assert.Empty(Server.InternalMessages.GetMessages<PlayerKingdomCreated>());
        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestCreateKingdom>());
    }

    [Fact]
    public void CreateKingdomDebugCommand_RejectsHeroThatDoesNotLeadItsClan()
    {
        var player = CreateSyncedPlayerContext();
        SetClanCultureEverywhere(player.ClanId, player.CultureId);
        var followerId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(followerId, out var follower));

            using (new AllowedThread())
            {
                follower.Clan = clan;
            }
        });

        string output = null;
        Server.Call(() =>
        {
            output = ExecuteCommand(
                new KingdomDebugCommand.KingdomCreateCoopCommand(),
                followerId,
                KingdomName);
        });

        Assert.Contains("does not lead clan", output);
        Assert.Empty(Server.InternalMessages.GetMessages<PlayerKingdomCreated>());

        Server.PumpGameThread();
    }

    [Fact]
    public void ForcePlayerJoinKingdom_UpdatesKingdomManagementCollections()
    {
        var player = CreateSyncedPlayerContext();
        var fiefId = CreateSyncedClanFief(player.ClanId);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var foundingClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        ConfigureClanInKingdom(foundingClanId, kingdomId);

        Server.Call(() =>
        {
            string result = ExecuteCommand(
                new KingdomDebugCommand.KingdomForcePlayerJoinCoopCommand(),
                ControllerId,
                kingdomId);

            Assert.Contains("Forced player", result);
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(foundingClanId, out var foundingClan));
            Assert.True(Server.ObjectManager.TryGetObject<Town>(fiefId, out var fief));

            Assert.Same(kingdom, clan.Kingdom);
            Assert.Same(kingdom, foundingClan.Kingdom);
            Assert.Contains(clan, kingdom.Clans);
            Assert.Contains(foundingClan, kingdom.Clans);
            Assert.Equal(2, kingdom.Clans.Count);
            Assert.Contains(fief, kingdom.Fiefs);
        });

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
                Assert.True(client.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));
                Assert.True(client.ObjectManager.TryGetObject<Clan>(foundingClanId, out var foundingClan));

                Assert.Same(kingdom, clan.Kingdom);
                Assert.Same(kingdom, foundingClan.Kingdom);
                Assert.Contains(clan, kingdom.Clans);
                Assert.Contains(foundingClan, kingdom.Clans);
                Assert.Equal(2, kingdom.Clans.Count);
                AssertKingdomContainsFief(client.ObjectManager, kingdom, fiefId);
            });
        }
    }

    [Fact]
    public void ForcePlayerJoinKingdom_RestoresKingdomlessClanThroughDirectMembershipState()
    {
        var player = CreateSyncedPlayerContext();
        var fiefId = CreateSyncedClanFief(player.ClanId);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        Server.Call(() =>
        {
            var joinResult = ExecuteCommand(new KingdomDebugCommand.KingdomForcePlayerJoinCoopCommand(), ControllerId, kingdomId);

            Assert.Contains("Forced player", joinResult);
        });

        Server.NetworkSentMessages.Clear();
        Server.Call(() =>
        {
            var restoreResult = ExecuteCommand(new KingdomDebugCommand.KingdomForcePlayerJoinCoopCommand(), ControllerId, "none");

            Assert.Contains("Restored player", restoreResult);
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));
            Assert.True(Server.ObjectManager.TryGetObject<Town>(fiefId, out var fief));

            Assert.Null(clan.Kingdom);
            Assert.DoesNotContain(clan, kingdom.Clans);
            Assert.DoesNotContain(fief, kingdom.Fiefs);
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkOnClanChangedKingdom>());

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
                Assert.True(client.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));
                Assert.True(client.ObjectManager.TryGetObject<Town>(fiefId, out var fief));

                Assert.Null(clan.Kingdom);
                Assert.DoesNotContain(clan, kingdom.Clans);
                Assert.DoesNotContain(fief, kingdom.Fiefs);
            });
        }
    }

    [Fact]
    public void VassalServiceAccepted_JoinsPlayerClanAuthoritativelyAndRejectsReplay()
    {
        var client = Clients.First();
        var player = CreateSyncedPlayerContext(ControllerId, client);
        var ruler = CreateSyncedPlayerContext("VassalRuler", _ => false);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        ConfigureClanInKingdom(ruler.ClanId, kingdomId);
        SetClanTierEverywhere(player.ClanId, 2);

        Server.Call(() => Server.Resolve<IPlayerManager>().SetPeer(ControllerId, client.NetPeer));

        Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
        client.SimulateMessage(this, new VassalServiceAccepted(kingdom, grantRewards: false));

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<RequestVassalService>());
        Assert.Equal(kingdomId, request.KingdomId);
        Assert.False(request.GrantRewards);

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<VassalServiceResult>());
        Assert.True(accepted.Accepted);
        Assert.False(accepted.GrantRewards);

        Server.Call(() => AssertVassalMembership(Server, player.ClanId, kingdomId));
        foreach (var instance in Clients)
        {
            instance.Call(() => AssertVassalMembership(instance, player.ClanId, kingdomId));
        }

        Server.NetworkSentMessages.Clear();
        client.SimulateMessage(this, new VassalServiceAccepted(kingdom, grantRewards: false));

        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<VassalServiceResult>());
        Assert.False(rejected.Accepted);

        Server.PumpGameThread();
    }
    
    [Fact]
    public void VassalServiceAccepted_DuringSettlementLocationConversation_JoinsKingdomAndReleasesConversationLock()
    {
        const string LocationId = "vassal_oath_settlement";

        var client = Clients.First();
        var joiner = CreateSyncedPlayerContext(ControllerId, client);
        var ruler = CreateSyncedPlayerContext("SettlementVassalRuler", _ => false);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        ConfigureClanInKingdom(ruler.ClanId, kingdomId);
        SetClanTierEverywhere(joiner.ClanId, 2);

        Server.Call(() => Server.Resolve<IPlayerManager>().SetPeer(ControllerId, client.NetPeer));

        var onAgentInteractionPrefix = AccessTools.Method(typeof(LocationConversationPatches), "OnAgentInteractionPrefix");
        var onConversationEndPostfix = AccessTools.Method(typeof(LocationConversationPatches), "OnConversationEndPostfix");
        ILocationConversationClientState clientConversationState = null;

        using var fixture = new MissionEngineFixture();
        var harmony = new Harmony($"e2e.vassal-oath-settlement.{Guid.NewGuid():N}");
        harmony.Patch(
            AccessTools.Method(typeof(Agent), nameof(Agent.IsEnemyOf)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(PlayerKingdomCreationFlowTests), nameof(NeverEnemiesPrefix))));
        harmony.Patch(
            AccessTools.PropertyGetter(typeof(MissionConversationLogic), nameof(MissionConversationLogic.Current)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(PlayerKingdomCreationFlowTests), nameof(GetMissionConversationLogicCurrentPrefix))));
        harmony.Patch(
            AccessTools.Method(typeof(MissionConversationLogic), nameof(MissionConversationLogic.StartConversation)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(PlayerKingdomCreationFlowTests), nameof(SuppressNativeStartConversationPrefix))));
        harmony.Patch(
            AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.OneToOneConversationHero)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(PlayerKingdomCreationFlowTests), nameof(GetOneToOneConversationHeroPrefix))));

        try
        {
            Agent joinerAgent = null;
            Agent rulerAgent = null;

            client.Call(() =>
            {
                var mock = fixture.CreateMission(client);

                Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(joiner.CharacterId, out var joinerCharacter));
                Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(ruler.CharacterId, out var rulerCharacter));
                Assert.True(rulerCharacter.IsHero);

                joinerAgent = mock.SpawnAgent(new AgentBuildData(joinerCharacter).Controller(AgentControllerType.Player));
                rulerAgent = mock.SpawnAgent(new AgentBuildData(rulerCharacter).Controller(AgentControllerType.AI));
                mock.MainAgent = joinerAgent;
                Assert.True(AgentMirror.TryGet(rulerAgent, out var rulerMirror));
                rulerMirror.Position = new Vec3(1f, 0f, 0f);

                Assert.NotNull(Campaign.Current.ConversationManager);
                MissionConversationLogicOverride = new MissionConversationLogic
                {
                    Mission = mock.Shell,
                    ConversationManager = Campaign.Current.ConversationManager,
                };

                var location = ObjectHelper.SkipConstructor<Location>();
                Assert.True(client.ObjectManager.AddExisting(LocationId, location));
                client.CampaignMissionContext = new StubCampaignMission(location);
                clientConversationState = client.Resolve<ILocationConversationClientState>();
                Assert.False(clientConversationState.HasPendingOrHeld);

                new LocationConversationTracker(client.ObjectManager);
            });
            
            Server.NetworkSentMessages.Clear();
            client.Call(() =>
            {
                var vanillaAllowed = (bool)onAgentInteractionPrefix.Invoke(
                    null, new object[] { MissionConversationLogicOverride, joinerAgent, rulerAgent });
                Assert.False(vanillaAllowed);
            });

            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAllowLocationConversation>());
            var started = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPlayerInteractionStarted>());
            Assert.True(started.IsLocationInteraction);

            // The approval round-trip runs synchronously through the mock network, so the scoped client state
            // has consumed its pending request and now holds the approved target.
            Assert.Equal(
                LocationConversationTracker.ComposeKey(LocationId, ruler.CharacterId),
                clientConversationState.HeldNpcKey);
            Server.Call(() =>
            {
                Assert.True(Server.Resolve<LocationConversationTracker>().TryGetEngagement(client.NetPeer, out var lockedNpcKey));
                Assert.Equal(LocationConversationTracker.ComposeKey(LocationId, ruler.CharacterId), lockedNpcKey);
            });

            // While that settlement conversation is still held, the joiner takes the oath to join the ruler's
            // kingdom driven through the real dialogue-consequence patch.
            Assert.True(client.ObjectManager.TryGetObject<Hero>(ruler.HeroId, out var rulerHero));
            OneToOneConversationHeroOverride = rulerHero;
            Server.NetworkSentMessages.Clear();
            client.Call(() =>
            {
                var behavior = new LordConversationsCampaignBehavior { _receivedVassalRewards = true };
                var vanillaAllowed = VassalServiceConversationPatch.ConversationPlayerIsAcceptedAsVassalPrefix(behavior);
                Assert.False(vanillaAllowed);
            });

            var request = Assert.Single(client.NetworkSentMessages.GetMessages<RequestVassalService>());
            Assert.Equal(kingdomId, request.KingdomId);
            Assert.False(request.GrantRewards);

            var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<VassalServiceResult>());
            Assert.True(accepted.Accepted);

            Server.Call(() => AssertVassalMembership(Server, joiner.ClanId, kingdomId));
            foreach (var instance in Clients)
            {
                instance.Call(() => AssertVassalMembership(instance, joiner.ClanId, kingdomId));
            }

            // The dialog concludes and the settlement conversation ends normally afterward.
            Server.NetworkSentMessages.Clear();
            client.Call(() => onConversationEndPostfix.Invoke(null, null));

            Assert.Null(clientConversationState.HeldNpcKey);
            var ended = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPlayerInteractionEnded>());
            Assert.True(ended.IsLocationInteraction);
            Server.Call(() =>
            {
                Assert.False(Server.Resolve<LocationConversationTracker>().TryGetEngagement(client.NetPeer, out _));
            });

            // Reacquire the SAME target: if the server's engagement tracker had not actually released it (only
            // NetworkPlayerInteractionEnded had fired), this second request would come back denied instead.
            Server.NetworkSentMessages.Clear();
            client.Call(() =>
            {
                new LocationConversationTracker(client.ObjectManager);
                var vanillaAllowed = (bool)onAgentInteractionPrefix.Invoke(
                    null, new object[] { MissionConversationLogicOverride, joinerAgent, rulerAgent });
                Assert.False(vanillaAllowed);
            });

            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAllowLocationConversation>());
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkLocationConversationDenied>());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            MissionConversationLogicOverride = null;
            OneToOneConversationHeroOverride = null;
            clientConversationState?.Clear();
            client.CampaignMissionContext = null;
        }

        Server.PumpGameThread();
    }

    private static MissionConversationLogic MissionConversationLogicOverride;
    private static Hero OneToOneConversationHeroOverride;

    private static bool GetMissionConversationLogicCurrentPrefix(ref MissionConversationLogic __result)
    {
        __result = MissionConversationLogicOverride;
        return false;
    }

    private static bool GetOneToOneConversationHeroPrefix(ref Hero __result)
    {
        __result = OneToOneConversationHeroOverride;
        return false;
    }
    
    private static bool SuppressNativeStartConversationPrefix() => false;

    private static bool NeverEnemiesPrefix(Agent __instance, ref bool __result)
    {
        if (!AgentMirror.TryGet(__instance, out _)) return true;
        __result = false;
        return false;
    }
    
    private sealed class StubCampaignMission : ICampaignMission
    {
        public StubCampaignMission(Location location) => Location = location;

        public GameState State => null;
        public IMissionTroopSupplier AgentSupplier => null;
        public Location Location { get; set; }
        public Alley LastVisitedAlley { get; set; }
        public MissionMode Mode => MissionMode.StartUp;
        public void SetMissionMode(MissionMode newMode, bool atStart) { }
        public void OnCloseEncounterMenu() { }
        public bool AgentLookingAtAgent(IAgent agent1, IAgent agent2) => false;
        public void OnCharacterLocationChanged(LocationCharacter locationCharacter, Location fromLocation, Location toLocation) { }
        public void OnProcessSentence() { }
        public void OnConversationContinue() { }
        public bool CheckIfAgentCanFollow(IAgent agent) => false;
        public void AddAgentFollowing(IAgent agent) { }
        public bool CheckIfAgentCanUnFollow(IAgent agent) => false;
        public void RemoveAgentFollowing(IAgent agent) { }
        public void OnConversationPlay(string idleActionId, string idleFaceAnimId, string reactionId, string reactionFaceAnimId, string soundPath) { }
        public void OnConversationStart(IAgent agent, bool setActionsInstantly) { }
        public void OnConversationEnd(IAgent agent) { }
        public void EndMission() { }
        public void FadeOutCharacter(CharacterObject characterObject) { }
        public void OnGameStateChanged() { }
    }

    [Fact]
    public void ClanChangedFactionNotification_AllowsNullKingdomEndpoints()
    {
        var player = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));
        Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
        Server.SimulateMessage(
            this,
            new NotifyClanChangedFaction(
                clan,
                oldKingdom: null,
                newKingdom: kingdom,
                detail: ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom,
                showNotification: true));

        var factionChanged = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkNotifyClanChangedFaction>(),
            message => message.ClanId == player.ClanId);
        Assert.Null(factionChanged.OldKingdomId);
        Assert.Equal(kingdomId, factionChanged.NewKingdomId);

        Server.NetworkSentMessages.Clear();
        Server.SimulateMessage(
            this,
            new NotifyClanChangedFaction(
                clan,
                oldKingdom: kingdom,
                newKingdom: null,
                detail: ChangeKingdomAction.ChangeKingdomActionDetail.LeaveKingdom,
                showNotification: true));

        factionChanged = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkNotifyClanChangedFaction>(),
            message => message.ClanId == player.ClanId);
        Assert.Equal(kingdomId, factionChanged.OldKingdomId);
        Assert.Null(factionChanged.NewKingdomId);
    }

    [Fact]
    public void ForcePlayerJoinKingdom_MovesClanOutOfPreviousKingdomCollections()
    {
        var player = CreateSyncedPlayerContext();
        var fiefId = CreateSyncedClanFief(player.ClanId);
        var previousKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var newKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(previousKingdomId, out var previousKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));
            Assert.True(Server.ObjectManager.TryGetObject<Town>(fiefId, out var fief));

            using (new AllowedThread())
            {
                clan._kingdom = previousKingdom;
                previousKingdom._clans ??= new MBList<Clan>();
                previousKingdom._fiefsCache ??= new MBList<Town>();
                previousKingdom._townsCache ??= new MBList<Town>();

                previousKingdom._clans.Add(clan);
                previousKingdom._fiefsCache.Add(fief);
                previousKingdom._townsCache.Add(fief);
            }

            Assert.Same(previousKingdom, clan.Kingdom);
            Assert.Contains(clan, previousKingdom.Clans);
            Assert.Contains(fief, previousKingdom.Fiefs);
        });

        Server.Call(() =>
        {
            string result = ExecuteCommand(
                new KingdomDebugCommand.KingdomForcePlayerJoinCoopCommand(),
                ControllerId,
                newKingdomId);

            Assert.Contains("Forced player", result);
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(previousKingdomId, out var previousKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(newKingdomId, out var newKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));
            Assert.True(Server.ObjectManager.TryGetObject<Town>(fiefId, out var fief));

            Assert.Same(newKingdom, clan.Kingdom);
            Assert.DoesNotContain(clan, previousKingdom.Clans);
            Assert.DoesNotContain(fief, previousKingdom.Fiefs);
            Assert.Contains(clan, newKingdom.Clans);
            Assert.Contains(fief, newKingdom.Fiefs);
        });
    }

    [Fact]
    public void KingdomDecisionVotes_WaitForEveryPlayerClanBeforeResolvingDeclareWar()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdom", _ => false);

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            var decision = new DeclareWarDecision(proposerClan, targetKingdom);
            kingdom.AddDecision(decision);

            Assert.Single(kingdom.UnresolvedDecisions);
        });

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
                Assert.Single(kingdom.UnresolvedDecisions);
                Assert.IsType<DeclareWarDecision>(kingdom.UnresolvedDecisions[0]);
            });
        }

        var player1Preview = CreateDeclareWarVote(kingdomId, isFinal: false);
        client1.SimulateMessage(this, new KingdomDecisionVoteRequested(player1Preview));

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>());

        var player1Final = CreateDeclareWarVote(kingdomId, isFinal: true);
        client1.SimulateMessage(this, new KingdomDecisionVoteRequested(player1Final));

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.Single(kingdom.UnresolvedDecisions);
        });

        var player2Preview = CreateDeclareWarVote(kingdomId, isFinal: false);
        client2.SimulateMessage(this, new KingdomDecisionVoteRequested(player2Preview));

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>());

        KingdomDecisionsVM activeDecisionsVm = null;
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            activeDecisionsVm = new KingdomDecisionsVM(() => { });
            activeDecisionsVm.RefreshWith(decision);

            Assert.NotNull(activeDecisionsVm.CurrentDecision);
        });

        var player2Final = CreateDeclareWarVote(kingdomId, isFinal: true);
        client2.SimulateMessage(this, new KingdomDecisionVoteRequested(player2Final));

        var resolvedMessage = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>(),
            message => message.KingdomId == kingdomId
                       && message.DecisionIndex == 0
                       && message.OutcomeIndex == 0
                       && message.IsPlayerDecision);
        Assert.Contains("war", resolvedMessage.NotificationText, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(Server.InternalMessages.GetMessages<FactionWarDeclared>());
        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>(),
            message => message.Faction1Id == kingdomId
                       && message.Faction2Id == targetKingdomId
                       && message.Detail == (int)DeclareWarAction.DeclareWarDetail.CausedByKingdomDecision);

        foreach (var client in Clients)
        {
            Assert.Contains(
                client.InternalMessages.GetMessages<ApplyKingdomDecisionResolved>(),
                message => message.KingdomId == kingdomId
                           && message.DecisionIndex == 0
                           && message.OutcomeIndex == 0
                           && message.IsPlayerDecision
                           && message.NotificationText == resolvedMessage.NotificationText);
            Assert.Contains(
                client.InternalMessages.GetMessages<SendInformationMessage>(),
                message => message.Text == resolvedMessage.NotificationText);
            Assert.Contains(
                client.InternalMessages.GetMessages<DeclareWarChanged>(),
                message => message.Faction1Id == kingdomId
                           && message.Faction2Id == targetKingdomId
                           && message.Detail == (int)DeclareWarAction.DeclareWarDetail.CausedByKingdomDecision);
        }

        client1.Call(() => Assert.Null(activeDecisionsVm.CurrentDecision));
    }

    [Fact]
    public void KingdomDecisionVotes_FinalClanBallotCannotBeOverwritten()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomImmutableVote", _ => false);

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        client1.SimulateMessage(
            this,
            new KingdomDecisionVoteRequested(CreateDeclareWarVote(kingdomId, isFinal: true)));
        client1.SimulateMessage(
            this,
            new KingdomDecisionVoteRequested(CreateDeclareWarNoVote(kingdomId, isFinal: true)));

        NetworkChangeKingdomDecisionVote player1Vote = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkChangeKingdomDecisionVote>(),
            message => message.ClanId == player1.ClanId);
        Assert.Equal(0, player1Vote.VoteData.OutcomeIndex);

        client2.SimulateMessage(
            this,
            new KingdomDecisionVoteRequested(CreateDeclareWarVote(kingdomId, isFinal: true)));

        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>(),
            message => message.KingdomId == kingdomId && message.OutcomeIndex == 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KingdomDecisionVotes_CoopClanRequiresOnlyCurrentLeadersBallot(bool changeLeader)
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContextInClan(SecondControllerId, client2, player1.ClanId);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomSharedClanVote", _ => false);

        Assert.Equal(player1.ClanId, player2.ClanId);
        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        var initialStatus = Server.NetworkSentMessages
            .GetMessages<NetworkKingdomDecisionRoundStatus>()
            .Last(message => message.Status.KingdomId == kingdomId)
            .Status;
        KingdomDecisionRoundClanStatusData requiredClan = Assert.Single(initialStatus.Clans);
        Assert.Equal(player1.ClanId, requiredClan.ClanId);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player1.HeroId, out var leader));
            Assert.Equal(leader.Name.ToString(), requiredClan.PlayerNames);
            Server.Resolve<IPlayerManager>().ClearPeer(client1.NetPeer);
            GetConcreteVoteManager(Server).ProcessVotingRounds(DateTime.UtcNow);
        });

        var disconnectedStatus = Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionRoundStatus>()
            .Last(message => message.Status.KingdomId == kingdomId).Status;
        Assert.False(Assert.Single(disconnectedStatus.Clans).IsConnected);
        Assert.Equal(initialStatus.DeadlineUtcTicks, disconnectedStatus.DeadlineUtcTicks);
        TestEnvironment.ConnectRegisteredPlayer(client1, ControllerId);

        KingdomDecisionVoteData sharedClanVote = CreateDeclareWarVote(kingdomId, isFinal: true);
        client2.SimulateMessage(this, new KingdomDecisionVoteRequested(sharedClanVote));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkChangeKingdomDecisionVote>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
        client2.Call(() =>
        {
            Assert.True(client2.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var voteManager = GetVoteManager(client2);
            voteManager.RegisterDecision(decision);
            Assert.True(voteManager.ShouldSuppressLocalDecision(decision));
            Assert.False(KingdomDecisionsVMPatches.RefreshWithPrefix(null, decision));
            voteManager.ApplyRemoteVote(player1.ClanId, sharedClanVote);

            Assert.True(voteManager.HasLocalPlayerSubmittedVote(decision));
        });

        if (changeLeader)
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(player2.HeroId, out var successor));
                successor.Clan._leader = successor;
                GetConcreteVoteManager(Server).ProcessVotingRounds(DateTime.UtcNow);
                var status = GetVoteManager(Server).CaptureActiveRoundStatuses().Single();
                Assert.Equal(successor.Name.ToString(), Assert.Single(status.Clans).PlayerNames);
                Assert.Equal(initialStatus.DeadlineUtcTicks, status.DeadlineUtcTicks);
            });
            client1.SimulateMessage(this, new KingdomDecisionVoteRequested(sharedClanVote));
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkChangeKingdomDecisionVote>());
        }

        (changeLeader ? client2 : client1).SimulateMessage(this, new KingdomDecisionVoteRequested(sharedClanVote));

        Server.PumpGameThread();
        foreach (var client in Clients) client.PumpGameThread();

        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkChangeKingdomDecisionVote>(),
            message => message.ClanId == player1.ClanId);
        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>(),
            message => message.KingdomId == kingdomId && message.OutcomeIndex == 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KingdomDecisionVotes_CoopClanEligibilityRequiresConnectedLeader(bool leaderConnected)
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        var leader = CreateSyncedPlayerContext(ControllerId, client1);
        CreateSyncedPlayerContextInClan(SecondControllerId, client2, leader.ClanId);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        ConfigureClanInKingdom(leader.ClanId, kingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(leader.ClanId, out var clan));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            if (!leaderConnected) Server.Resolve<IPlayerManager>().ClearPeer(client1.NetPeer);

            var decision = new DeclareWarDecision(clan, targetKingdom);
            Assert.Equal(leaderConnected, GetVoteManager(Server).HasEligiblePlayerClan(decision));
        });

        Server.PumpGameThread();
        foreach (var client in Clients) client.PumpGameThread();
    }

    [Fact]
    public void KingdomDecisionVotes_OfflineRegisteredClanDoesNotBlockConnectedClan()
    {
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        var connectedPlayer = CreateSyncedPlayerContext(ControllerId, client);
        var offlinePlayer = CreateSyncedPlayerContext(SecondControllerId, _ => false);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomOfflineVoter", _ => false);

        ConfigureClanInKingdom(connectedPlayer.ClanId, kingdomId);
        ConfigureClanInKingdom(offlinePlayer.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(connectedPlayer.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        var initialStatus = Server.NetworkSentMessages
            .GetMessages<NetworkKingdomDecisionRoundStatus>()
            .Last(message => message.Status.KingdomId == kingdomId)
            .Status;
        Assert.Single(initialStatus.Clans);
        Assert.Equal(connectedPlayer.ClanId, initialStatus.Clans[0].ClanId);
        Assert.DoesNotContain(initialStatus.Clans, clan => clan.ClanId == offlinePlayer.ClanId);

        client.SimulateMessage(
            this,
            new KingdomDecisionVoteRequested(CreateDeclareWarVote(kingdomId, isFinal: true)));

        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>(),
            message => message.KingdomId == kingdomId);
    }

    [Fact]
    public void KingdomDecisionVotes_AfkClanTimesOutAtFixedRoundDeadline()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomAfkVoter", _ => false);

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        client1.SimulateMessage(
            this,
            new KingdomDecisionVoteRequested(CreateDeclareWarVote(kingdomId, isFinal: true)));

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
        var waitingStatus = Server.NetworkSentMessages
            .GetMessages<NetworkKingdomDecisionRoundStatus>()
            .Last(message => message.Status.KingdomId == kingdomId)
            .Status;
        var waitingClan = Assert.Single(waitingStatus.Clans, clan => clan.ClanId == player2.ClanId);
        Assert.False(waitingClan.HasFinalVote);
        Assert.True(waitingClan.IsConnected);

        Server.Call(() =>
        {
            GetConcreteVoteManager(Server).ProcessVotingRounds(
                DateTime.UtcNow + KingdomDecisionVoteManager.VotingRoundDuration + TimeSpan.FromSeconds(1));
        });

        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>(),
            message => message.KingdomId == kingdomId);
    }

    [Fact]
    public void KingdomDecisionVotes_RestoredDecisionGetsNewFixedRound()
    {
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        var player = CreateSyncedPlayerContext(ControllerId, client);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomRestoredRound", _ => false);

        ConfigureClanInKingdom(player.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        int statusCountBeforeReset = Server.NetworkSentMessages
            .GetMessages<NetworkKingdomDecisionRoundStatus>()
            .Count(message => message.Status.KingdomId == kingdomId);

        Server.Call(() =>
        {
            KingdomDecisionVoteManager voteManager = GetConcreteVoteManager(Server);
            voteManager.Reset();
            voteManager.ProcessVotingRounds(DateTime.UtcNow);
        });

        KingdomDecisionRoundStatusData restoredStatus = Server.NetworkSentMessages
            .GetMessages<NetworkKingdomDecisionRoundStatus>()
            .Where(message => message.Status.KingdomId == kingdomId)
            .Skip(statusCountBeforeReset)
            .Last()
            .Status;
        Assert.True(restoredStatus.DeadlineUtcTicks > DateTime.UtcNow.Ticks);
        Assert.Single(restoredStatus.Clans);

        Server.Call(() =>
        {
            GetConcreteVoteManager(Server).ProcessVotingRounds(
                new DateTime(restoredStatus.DeadlineUtcTicks, DateTimeKind.Utc) + TimeSpan.FromSeconds(1));
        });

        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>(),
            message => message.KingdomId == kingdomId);
    }

    [Fact]
    public void KingdomDecisionVotes_LateJoinerDoesNotEnterRoundQuorum()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, _ => false);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomLateJoiner", _ => false);

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        TestEnvironment.ConnectRegisteredPlayer(client2, SecondControllerId);
        Server.Call(() => GetConcreteVoteManager(Server).ProcessVotingRounds(DateTime.UtcNow));

        var statusAfterJoin = Server.NetworkSentMessages
            .GetMessages<NetworkKingdomDecisionRoundStatus>()
            .Last(message => message.Status.KingdomId == kingdomId)
            .Status;
        Assert.Single(statusAfterJoin.Clans);
        Assert.Equal(player1.ClanId, statusAfterJoin.Clans[0].ClanId);

        client1.SimulateMessage(
            this,
            new KingdomDecisionVoteRequested(CreateDeclareWarVote(kingdomId, isFinal: true)));

        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>(),
            message => message.KingdomId == kingdomId);
    }

    [Fact]
    public void KingdomDecisionVotes_DisconnectStatusDoesNotExtendDeadline()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomDisconnect", _ => false);

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        KingdomDecisionRoundStatusData initialStatus = Server.NetworkSentMessages
            .GetMessages<NetworkKingdomDecisionRoundStatus>()
            .Last(message => message.Status.KingdomId == kingdomId)
            .Status;

        Server.Call(() =>
        {
            Server.Resolve<IPlayerManager>().ClearPeer(client2.NetPeer);
            GetConcreteVoteManager(Server).ProcessVotingRounds(DateTime.UtcNow + TimeSpan.FromSeconds(5));
        });

        KingdomDecisionRoundStatusData disconnectedStatus = Server.NetworkSentMessages
            .GetMessages<NetworkKingdomDecisionRoundStatus>()
            .Last(message => message.Status.KingdomId == kingdomId)
            .Status;
        KingdomDecisionRoundClanStatusData disconnectedClan = Assert.Single(
            disconnectedStatus.Clans,
            clan => clan.ClanId == player2.ClanId);
        Assert.False(disconnectedClan.IsConnected);
        Assert.Equal(initialStatus.DeadlineUtcTicks, disconnectedStatus.DeadlineUtcTicks);

        TestEnvironment.ConnectRegisteredPlayer(client2, SecondControllerId);
        Server.Call(() => GetConcreteVoteManager(Server).ProcessVotingRounds(DateTime.UtcNow + TimeSpan.FromSeconds(10)));

        KingdomDecisionRoundStatusData reconnectedStatus = Server.NetworkSentMessages
            .GetMessages<NetworkKingdomDecisionRoundStatus>()
            .Last(message => message.Status.KingdomId == kingdomId)
            .Status;
        Assert.True(Assert.Single(reconnectedStatus.Clans, clan => clan.ClanId == player2.ClanId).IsConnected);
        Assert.Equal(initialStatus.DeadlineUtcTicks, reconnectedStatus.DeadlineUtcTicks);
    }

    [Fact]
    public void KingdomDecisionVotes_UnchangedRoundTickDoesNotRepublishStatus()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomUnchangedTick", _ => false);

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        int statusCount = Server.NetworkSentMessages
            .GetMessages<NetworkKingdomDecisionRoundStatus>()
            .Count(message => message.Status.KingdomId == kingdomId);
        Assert.True(statusCount > 0);

        Server.Call(() => GetConcreteVoteManager(Server).ProcessVotingRounds(DateTime.UtcNow + TimeSpan.FromSeconds(1)));

        Assert.Equal(
            statusCount,
            Server.NetworkSentMessages
                .GetMessages<NetworkKingdomDecisionRoundStatus>()
                .Count(message => message.Status.KingdomId == kingdomId));
    }

    [Fact]
    public void KingdomDecisionFinalVote_ClosesPanelAndReopensWithWaitingFeedback()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomWaitingUi", _ => false);

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client1.ObjectManager.TryGetObject<Clan>(player2.ClanId, out var remoteChooserClan));
            Assert.True(client1.ObjectManager.TryGetObject<MobileParty>(player2.PartyId, out var remoteChooserParty));
            Assert.True(client1.ObjectManager.TryGetObject<Hero>(player2.HeroId, out var waitingHero));
            using (new AllowedThread())
            {
                remoteChooserParty._partyComponent = new LordPartyComponent(waitingHero, waitingHero, null);
            }
            Assert.Same(waitingHero, remoteChooserParty.LeaderHero);
            Assert.True(remoteChooserParty.IsPlayerParty());
            Assert.True(waitingHero.IsHumanPlayerCharacter);
            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var decisionsVm = new KingdomDecisionsVM(() => { });
            decisionsVm.RefreshWith(decision);
            var decisionItem = decisionsVm.CurrentDecision;
            Clan originalChooser = decisionItem.KingdomDecisionMaker._chooser;
            decisionItem.KingdomDecisionMaker._chooser = remoteChooserClan;
            Assert.NotSame(Clan.PlayerClan, remoteChooserClan);
            Assert.True(decisionItem.KingdomDecisionMaker.IsPlayerChooser);
            string supportTitle = decision.GetSupportTitle().ToString();
            string chooseTitle = decision.GetChooseTitle().ToString();
            Assert.NotEqual(chooseTitle, supportTitle);
            string remoteChooserTitle = GetVoteManager(client1).RefreshDecisionTitle(decisionItem);
            Assert.StartsWith(supportTitle.TrimEnd('.'), remoteChooserTitle);
            Assert.DoesNotContain(chooseTitle, remoteChooserTitle);
            decisionItem.KingdomDecisionMaker._chooser = originalChooser;
            GetVoteManager(client1).RefreshDecisionTitle(decisionItem);
            DecisionOptionVM option = decisionItem.DecisionOptionsList.Single(candidate =>
                IsDeclareWarOutcome(candidate.Option, true));
            option.CurrentSupportWeight = Supporter.SupportWeights.FullyPush;
            decisionItem._currentSelectedOption = option;
            string decisionTitle = decisionItem.TitleText;
            string decisionDescription = decisionItem.DescriptionText;

            decisionItem.ExecuteFinalSelection();

            Assert.Null(decisionsVm.CurrentDecision);
            Assert.False(decisionItem.IsActive);
            Assert.True(decisionItem._finalSelectionDone);
            Assert.False(decisionItem.CanEndDecision);
            Assert.Equal(decisionDescription, decisionItem.DescriptionText);
            string titled = GetVoteManager(client1).RefreshDecisionTitle(decisionItem);
            string titledAgain = GetVoteManager(client1).RefreshDecisionTitle(decisionItem);
            Assert.Equal(titled, titledAgain);
            Assert.Contains(decisionTitle.TrimEnd('.'), titled);
            Assert.Contains("Voting ends in", titled);
            Assert.Equal(titled.IndexOf("Voting ends in", StringComparison.Ordinal), titled.LastIndexOf("Voting ends in", StringComparison.Ordinal));
            string waitingStatus = GetVoteManager(client1).RefreshDecisionWaitingStatus(decisionItem);
            IReadOnlyList<string> waitingColumns = GetVoteManager(client1).GetDecisionWaitingColumns(decisionItem);
            Assert.Contains("Vote submitted", waitingStatus);
            Assert.DoesNotContain("Voting ends in", waitingStatus);
            Assert.Contains("Waiting for", waitingStatus);
            Assert.Contains(waitingHero.Name.ToString(), string.Join("\n", waitingColumns));
            Assert.Equal(4, waitingColumns.Count);

            var reopenedDecisionsVm = new KingdomDecisionsVM(() => { });
            reopenedDecisionsVm.RefreshWith(decision);
            DecisionItemBaseVM reopenedDecisionItem = reopenedDecisionsVm.CurrentDecision;

            Assert.True(reopenedDecisionItem._finalSelectionDone);
            Assert.True(reopenedDecisionItem.CanEndDecision);
            Assert.Equal(decisionDescription, reopenedDecisionItem.DescriptionText);
            Assert.Contains("Voting ends in", GetVoteManager(client1).RefreshDecisionTitle(reopenedDecisionItem));
            string reopenedWaitingStatus = GetVoteManager(client1).RefreshDecisionWaitingStatus(reopenedDecisionItem);
            Assert.Contains("Vote submitted", reopenedWaitingStatus);
            Assert.DoesNotContain("Voting ends in", reopenedWaitingStatus);
            Assert.Contains(waitingHero.Name.ToString(), string.Join("\n", GetVoteManager(client1).GetDecisionWaitingColumns(reopenedDecisionItem)));
            Assert.All(reopenedDecisionItem.DecisionOptionsList, candidate => Assert.False(candidate.CanBeChosen));

            reopenedDecisionItem.ExecuteFinalSelection();

            Assert.Null(reopenedDecisionsVm.CurrentDecision);
            Assert.False(reopenedDecisionItem.IsActive);
        });

        Assert.Single(client1.NetworkSentMessages.GetMessages<NetworkRequestKingdomDecisionVote>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
    }

    [Fact]
    public void KingdomDecisionRemoval_ClosesActiveModalBeforeRemovingDecisionState()
    {
        var client = Clients.Skip(1).First();
        client.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var proposer = CreateSyncedPlayerContext(ControllerId, Clients.First());
        var player = CreateSyncedPlayerContext(SecondControllerId, client);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(proposer.ClanId, kingdomId);
        ConfigureClanInKingdom(player.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client.ObjectManager.TryGetObject<Clan>(proposer.ClanId, out var proposerClan));
            PolicyObject policy = PolicyObject.All.First(candidate => !kingdom.ActivePolicies.Contains(candidate));
            using (new AllowedThread())
            {
                kingdom._unresolvedDecisions.Add(new KingdomPolicyDecision(proposerClan, policy, false));
            }

            var decision = Assert.IsType<KingdomPolicyDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var decisionsVm = new KingdomDecisionsVM(() => { });
            decisionsVm.RefreshWith(decision);

            Assert.NotNull(decisionsVm.CurrentDecision);
            client.SimulateMessage(this, new NetworkRemoveDecision(kingdomId, 0));

            Assert.Null(decisionsVm.CurrentDecision);
            Assert.Empty(kingdom.UnresolvedDecisions);
        });
    }

    [Fact]
    public void KingdomDecisionResolution_DoesNotReopenBeforeRemovalApplies()
    {
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        var player = CreateSyncedPlayerContext(ControllerId, client);
        var otherPlayer = CreateSyncedPlayerContext("PolicyTimeoutOtherClan", _ => false);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player.ClanId, kingdomId);
        ConfigureClanInKingdom(otherPlayer.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client.ObjectManager.TryGetObject<Clan>(player.ClanId, out var proposerClan));
            PolicyObject policy = PolicyObject.All.First(candidate => !kingdom.ActivePolicies.Contains(candidate));
            using (new AllowedThread())
            {
                kingdom._unresolvedDecisions.Add(new KingdomPolicyDecision(proposerClan, policy, false));
            }

            var decision = Assert.IsType<KingdomPolicyDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var decisionsVm = new KingdomDecisionsVM(() => { });
            decisionsVm.RefreshWith(decision);

            Assert.NotNull(decisionsVm.CurrentDecision);
            GetVoteManager(client).ApplyResolved(kingdomId, 0, 0, true);

            Assert.Null(decisionsVm.CurrentDecision);
            Assert.True(GetVoteManager(client).ShouldSuppressLocalDecision(decision));

            InquiryData reopenedInquiry = null;
            Action<InquiryData, bool, bool> onShowInquiry = (data, _, _) => reopenedInquiry = data;
            EventInfo showInquiryEvent = typeof(InformationManager).GetEvent(
                "OnShowInquiry",
                BindingFlags.Public | BindingFlags.Static);
            showInquiryEvent.AddEventHandler(null, onShowInquiry);
            try
            {
                decisionsVm.OnFrameTick();

                Assert.Null(reopenedInquiry);
                Assert.Null(decisionsVm.CurrentDecision);
                Assert.Contains(decision, decisionsVm._examinedDecisionsSinceInit);
            }
            finally
            {
                showInquiryEvent.RemoveEventHandler(null, onShowInquiry);
            }

            client.SimulateMessage(this, new NetworkRemoveDecision(kingdomId, 0));

            Assert.Empty(kingdom.UnresolvedDecisions);
            Assert.False(GetVoteManager(client).ShouldSuppressLocalDecision(decision));
        });
    }

    [Fact]
    public void SingleClanKingdomDecisionResolution_DoesNotReopenBeforeRemovalApplies()
    {
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        var player = CreateSyncedPlayerContext(ControllerId, client);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client.ObjectManager.TryGetObject<Clan>(player.ClanId, out var proposerClan));
            PolicyObject policy = PolicyObject.All.First(candidate => !kingdom.ActivePolicies.Contains(candidate));
            using (new AllowedThread())
            {
                kingdom._unresolvedDecisions.Add(new KingdomPolicyDecision(proposerClan, policy, false));
            }

            var decision = Assert.IsType<KingdomPolicyDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var decisionsVm = new KingdomDecisionsVM(() => { });
            var shownInquiries = new List<InquiryData>();
            Action<InquiryData, bool, bool> onShowInquiry = (data, _, _) => shownInquiries.Add(data);
            EventInfo showInquiryEvent = typeof(InformationManager).GetEvent(
                "OnShowInquiry",
                BindingFlags.Public | BindingFlags.Static);
            showInquiryEvent.AddEventHandler(null, onShowInquiry);
            try
            {
                decisionsVm.HandleDecision(decision);

                // Outcome inquiry for the single-clan resolution should have fired exactly once.
                Assert.Single(shownInquiries);

                GetVoteManager(client).ApplyResolved(kingdomId, 0, 0, true);

                Assert.Null(decisionsVm.CurrentDecision);
                Assert.True(GetVoteManager(client).ShouldSuppressLocalDecision(decision));

                decisionsVm.OnFrameTick();

                // Must not have reopened the inquiry in the resolve/remove gap.
                Assert.Single(shownInquiries);
                Assert.Null(decisionsVm.CurrentDecision);
                Assert.Contains(decision, decisionsVm._examinedDecisionsSinceInit);
            }
            finally
            {
                showInquiryEvent.RemoveEventHandler(null, onShowInquiry);
            }

            client.SimulateMessage(this, new NetworkRemoveDecision(kingdomId, 0));

            Assert.Empty(kingdom.UnresolvedDecisions);
            Assert.False(GetVoteManager(client).ShouldSuppressLocalDecision(decision));
        });
    }

    [Fact]
    public void SingleClanKingdomDecisionResolution_DoesNotProcessNextDecisionWhileOutcomeInquiryPending()
    {
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        var player = CreateSyncedPlayerContext(ControllerId, client);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client.ObjectManager.TryGetObject<Clan>(player.ClanId, out var proposerClan));

            PolicyObject firstPolicy = PolicyObject.All.First(c => !kingdom.ActivePolicies.Contains(c));
            PolicyObject secondPolicy = PolicyObject.All.First(c => c != firstPolicy && !kingdom.ActivePolicies.Contains(c));

            using (new AllowedThread())
            {
                kingdom._unresolvedDecisions.Add(new KingdomPolicyDecision(proposerClan, firstPolicy, false));
                kingdom._unresolvedDecisions.Add(new KingdomPolicyDecision(proposerClan, secondPolicy, false));
            }

            var firstDecision = kingdom.UnresolvedDecisions[0];
            var secondDecision = kingdom.UnresolvedDecisions[1];
            var decisionsVm = new KingdomDecisionsVM(() => { });

            var shownInquiries = new List<InquiryData>();
            Action<InquiryData, bool, bool> onShowInquiry = (data, _, _) => shownInquiries.Add(data);
            EventInfo showInquiryEvent = typeof(InformationManager).GetEvent("OnShowInquiry", BindingFlags.Public | BindingFlags.Static);
            showInquiryEvent.AddEventHandler(null, onShowInquiry);
            try
            {
                decisionsVm.HandleDecision(firstDecision);

                // Outcome inquiry for firstDecision is up; OnSingleDecisionOver hasn't fired,
                // so _shouldCheckForDecision must still be false here.
                Assert.Single(shownInquiries);
                Assert.Contains(firstDecision, decisionsVm._examinedDecisionsSinceInit);
                Assert.DoesNotContain(secondDecision, decisionsVm._examinedDecisionsSinceInit);

                // A frame tick landing in this gap must not pick up secondDecision.
                decisionsVm.OnFrameTick();

                Assert.Single(shownInquiries);
                Assert.Null(decisionsVm.CurrentDecision);
                Assert.DoesNotContain(secondDecision, decisionsVm._examinedDecisionsSinceInit);

                //Player clicking OK, thus OnSingleDecision runs
                shownInquiries[0].AffirmativeAction?.Invoke();

                decisionsVm.OnFrameTick(); // calls HandleNextDecision into HandleDecision since shouldCheckForDecision is now true

                Assert.Equal(2, shownInquiries.Count);
                Assert.Contains(secondDecision, decisionsVm._examinedDecisionsSinceInit);
            }
            finally
            {
                showInquiryEvent.RemoveEventHandler(null, onShowInquiry);
            }
        });
    }

    [Fact]
    public void SingleClanKingdomDecisionResolution_DoesNotProcessDecisionWhenMapActionsDisabled()
    {
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        var player = CreateSyncedPlayerContext(ControllerId, client);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client.ObjectManager.TryGetObject<Clan>(player.ClanId, out var proposerClan));
            PolicyObject policy = PolicyObject.All.First(candidate => !kingdom.ActivePolicies.Contains(candidate));
            using (new AllowedThread())
            {
                kingdom._unresolvedDecisions.Add(new KingdomPolicyDecision(proposerClan, policy, false));
            }

            var decision = Assert.IsType<KingdomPolicyDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var decisionsVm = new KingdomDecisionsVM(() => { });

            var shownInquiries = new List<InquiryData>();
            Action<InquiryData, bool, bool> onShowInquiry = (data, _, _) => shownInquiries.Add(data);
            EventInfo showInquiryEvent = typeof(InformationManager).GetEvent(
                "OnShowInquiry",
                BindingFlags.Public | BindingFlags.Static);
            showInquiryEvent.AddEventHandler(null, onShowInquiry);
            try
            {
                using (new AllowedThread())
                {
                    Hero.MainHero.ChangeState(Hero.CharacterStates.Prisoner);
                }

                decisionsVm.HandleDecision(decision);

                // Map actions disabled, thus prefix's guard clause must return true (fall through to
                // vanilla HandleDecision), which also bails out on the disabled-reason check
                // and never touches the decision.
                Assert.Empty(shownInquiries);
                Assert.Null(decisionsVm.CurrentDecision);
                Assert.DoesNotContain(decision, decisionsVm._examinedDecisionsSinceInit);
            }
            finally
            {
                showInquiryEvent.RemoveEventHandler(null, onShowInquiry);
                using (new AllowedThread())
                {
                    Hero.MainHero.ChangeState(Hero.CharacterStates.Active);
                }
            }
        });
    }

    [Fact]
    public void KingdomDecisionRoundStatus_ShowsClosableWaitingViewWhenLocalClanAlreadySubmitted()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomSubmittedStatus", _ => false);

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var decisionsVm = new KingdomDecisionsVM(() => { });
            decisionsVm.RefreshWith(decision);
            DecisionItemBaseVM decisionItem = decisionsVm.CurrentDecision;
            string decisionDescription = decisionItem.DescriptionText;

            GetVoteManager(client1).ApplyRoundStatus(new KingdomDecisionRoundStatusData(
                kingdomId,
                0,
                (DateTime.UtcNow + TimeSpan.FromSeconds(60)).Ticks,
                new[]
                {
                    new KingdomDecisionRoundClanStatusData(
                        player1.ClanId,
                        player1.ClanId,
                        ControllerId,
                        true,
                        true),
                    new KingdomDecisionRoundClanStatusData(
                        player2.ClanId,
                        player2.ClanId,
                        SecondControllerId,
                        false,
                        true),
                }));

            Assert.True(GetVoteManager(client1).HasLocalPlayerSubmittedVote(decision));
            Assert.True(decisionItem._finalSelectionDone);
            Assert.True(decisionItem.CanEndDecision);
            Assert.Equal(decisionDescription, decisionItem.DescriptionText);
            Assert.Contains("Vote submitted", GetVoteManager(client1).RefreshDecisionWaitingStatus(decisionItem));
            Assert.DoesNotContain("Voting ends in", GetVoteManager(client1).RefreshDecisionWaitingStatus(decisionItem));
            Assert.All(decisionItem.DecisionOptionsList, candidate => Assert.False(candidate.CanBeChosen));

            decisionItem.ExecuteFinalSelection();

            Assert.Null(decisionsVm.CurrentDecision);
            Assert.False(decisionItem.IsActive);
        });

        Assert.Empty(client1.NetworkSentMessages.GetMessages<NetworkRequestKingdomDecisionVote>());
    }

    [Fact]
    public void KingdomDecisionFinalVote_ClickingSubmittedDecisionReopensClosableWaitingView()
    {
        var (client1, _, _, _, kingdomId) = SetUpPlayerDeclareWarVotes("TargetKingdomReopenClick");

        IReadOnlyList<string> lines = CaptureKingdomVoteLog(() => client1.Call(() =>
        {
            KingdomDecision decision = Assert.Single(Clan.PlayerClan.Kingdom.UnresolvedDecisions);
            var decisionsVm = new KingdomDecisionsVM(() => { });
            SubmitDeclareWarVoteFromPanel(decisionsVm, decision);
            Assert.Null(decisionsVm.CurrentDecision);

            // The vote notification and coop.debug.kingdom.open_decision open the screen through HandleDecision.
            var reopenedDecisionsVm = new KingdomDecisionsVM(() => { });
            InquiryData inquiry = Assert.Single(CaptureInquiries(() => reopenedDecisionsVm.HandleDecision(decision)));
            inquiry.AffirmativeAction();
            DecisionItemBaseVM reopenedDecisionItem = reopenedDecisionsVm.CurrentDecision;

            Assert.NotNull(reopenedDecisionItem);
            Assert.True(reopenedDecisionItem.IsActive);
            Assert.True(reopenedDecisionItem._finalSelectionDone);
            Assert.True(reopenedDecisionItem.CanEndDecision);
            Assert.Contains("Your vote is submitted", reopenedDecisionItem.EndDecisionHint.HintText.ToString());
            Assert.All(reopenedDecisionItem.DecisionOptionsList, candidate => Assert.False(candidate.CanBeChosen));
            string waitingStatus = GetVoteManager(client1).RefreshDecisionWaitingStatus(reopenedDecisionItem);
            Assert.Contains("Vote submitted", waitingStatus);
            Assert.Contains("Waiting for remaining players", waitingStatus);
            Assert.False(string.IsNullOrWhiteSpace(
                string.Join(string.Empty, GetVoteManager(client1).GetDecisionWaitingColumns(reopenedDecisionItem))));

            reopenedDecisionItem.ExecuteFinalSelection();

            Assert.Null(reopenedDecisionsVm.CurrentDecision);
            Assert.False(reopenedDecisionItem.IsActive);
            Assert.Empty(CaptureInquiries(reopenedDecisionsVm.OnFrameTick));
        }));

        Assert.Single(lines, line => line.Contains("Kingdom decision final selection"));
        Assert.Equal(2, lines.Count(line => line.Contains("panel closed after the vote was submitted")));
        Assert.Single(client1.NetworkSentMessages.GetMessages<NetworkRequestKingdomDecisionVote>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.Single(kingdom.UnresolvedDecisions);
        });
    }

    [Fact]
    public void KingdomDecisionFinalVote_ClosedSubmittedDecisionIsNotPromptedAgain()
    {
        var (client1, _, player1, _, kingdomId) = SetUpPlayerDeclareWarVotes("TargetKingdomNoReprompt");
        KingdomDecisionsVM decisionsVm = null;

        client1.Call(() =>
        {
            KingdomDecision decision = Assert.Single(Clan.PlayerClan.Kingdom.UnresolvedDecisions);
            decisionsVm = new KingdomDecisionsVM(() => { });
            // Kingdom tabs and the proposer's own panel open through RefreshWith, which does not mark it examined.
            SubmitDeclareWarVoteFromPanel(decisionsVm, decision);

            Assert.Empty(CaptureInquiries(decisionsVm.OnFrameTick));
            Assert.Contains(decision, decisionsVm._examinedDecisionsSinceInit);

            var reopenedScreenDecisionsVm = new KingdomDecisionsVM(() => { });
            Assert.Empty(CaptureInquiries(reopenedScreenDecisionsVm.OnFrameTick));
            Assert.Null(reopenedScreenDecisionsVm.CurrentDecision);

            // The army tab refresh asks for the next decision without a frame tick first.
            var armyRefreshDecisionsVm = new KingdomDecisionsVM(() => { });
            Assert.Empty(CaptureInquiries(armyRefreshDecisionsVm.HandleNextDecision));
            Assert.Null(armyRefreshDecisionsVm.CurrentDecision);
        });

        // A decision proposed later while the same kingdom screen stays open still prompts.
        var laterTargetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var laterTargetPlayer = CreateSyncedPlayerContext("TargetKingdomNoRepromptLater", _ => false);
        ConfigureClanInKingdom(laterTargetPlayer.ClanId, laterTargetKingdomId);
        EnsureKingdomRegisteredEverywhere(laterTargetKingdomId);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(laterTargetKingdomId, out var laterTargetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));
            kingdom.AddDecision(new DeclareWarDecision(proposerClan, laterTargetKingdom));
        });

        client1.Call(() =>
        {
            Assert.Equal(2, Clan.PlayerClan.Kingdom.UnresolvedDecisions.Count);
            KingdomDecision laterDecision = Clan.PlayerClan.Kingdom.UnresolvedDecisions[1];

            InquiryData inquiry = Assert.Single(CaptureInquiries(decisionsVm.OnFrameTick));
            inquiry.AffirmativeAction();

            Assert.Same(laterDecision, decisionsVm.CurrentDecision.KingdomDecisionMaker._decision);
        });
    }

    [Fact]
    public void KingdomDecisionFinalVote_SecondQueuedDecisionPromptsAfterDismiss()
    {
        var (client1, _, _, _, _) = SetUpPlayerDeclareWarVotes("TargetKingdomQueued", decisionCount: 2);

        client1.Call(() =>
        {
            List<KingdomDecision> decisions = Clan.PlayerClan.Kingdom.UnresolvedDecisions.ToList();
            Assert.Equal(2, decisions.Count);
            var decisionsVm = new KingdomDecisionsVM(() => { });
            DecisionItemBaseVM firstDecisionItem = SubmitDeclareWarVoteFromPanel(decisionsVm, decisions[0]);

            InquiryData inquiry = Assert.Single(CaptureInquiries(decisionsVm.OnFrameTick));
            inquiry.AffirmativeAction();
            DecisionItemBaseVM secondDecisionItem = decisionsVm.CurrentDecision;

            Assert.Same(decisions[1], secondDecisionItem.KingdomDecisionMaker._decision);
            Assert.False(secondDecisionItem._finalSelectionDone);
            Assert.All(secondDecisionItem.DecisionOptionsList, candidate => Assert.True(candidate.CanBeChosen));

            // A late second click on the closed first panel must not close the second one.
            firstDecisionItem.ExecuteFinalSelection();

            Assert.Same(secondDecisionItem, decisionsVm.CurrentDecision);
            Assert.True(secondDecisionItem.IsActive);
        });
    }

    [Fact]
    public void KingdomDecisionFinalVote_VotedClaimantFollowUpGivesWayToNextBallot()
    {
        var (client1, client2, player1, player2, kingdomId) = SetUpPlayerDeclareWarVotes("TargetKingdomClaimantFollowUp", decisionCount: 2);
        LeadPlayerPartiesEverywhere(player1, player2);
        string settlementId = CreateSyncedKingdomTown(player2.ClanId, player2.CultureId);
        // The claimant merit reads map distances, which the test campaign has no map for.
        var harmony = new Harmony($"e2e.claimant-follow-up.{Guid.NewGuid():N}");
        harmony.Patch(
            AccessTools.Method(typeof(SettlementClaimantDecision), nameof(SettlementClaimantDecision.CalculateMeritOfOutcome)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(PlayerKingdomCreationFlowTests), nameof(FixedClaimantMeritPrefix))));

        try
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
                Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));
                Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                kingdom.AddDecision(new SettlementClaimantPreliminaryDecision(proposerClan, settlement));
            });
            KingdomDecisionsVM decisionsVm = null;

            // Both clans vote to give the fief to a new clan, so the server adds the claimant decision.
            client1.Call(() =>
            {
                decisionsVm = new KingdomDecisionsVM(() => { });
                decisionsVm.RefreshWith(Clan.PlayerClan.Kingdom.UnresolvedDecisions.OfType<SettlementClaimantPreliminaryDecision>().Single());
                SubmitCurrentDecisionVote(decisionsVm, IsFiefOwnerChangeOutcome);
            });
            client2.Call(() =>
            {
                var client2DecisionsVm = new KingdomDecisionsVM(() => { });
                client2DecisionsVm.RefreshWith(Clan.PlayerClan.Kingdom.UnresolvedDecisions.OfType<SettlementClaimantPreliminaryDecision>().Single());
                SubmitCurrentDecisionVote(client2DecisionsVm, IsFiefOwnerChangeOutcome);
            });

            client1.Call(() =>
            {
                MBReadOnlyList<KingdomDecision> decisions = Clan.PlayerClan.Kingdom.UnresolvedDecisions;
                Assert.Equal(3, decisions.Count);
                var warDecision = Assert.IsType<DeclareWarDecision>(decisions[0]);
                var laterWarDecision = Assert.IsType<DeclareWarDecision>(decisions[1]);
                var claimantDecision = Assert.IsType<SettlementClaimantDecision>(decisions[2]);
                var preliminaryDecision = Assert.IsType<SettlementClaimantPreliminaryDecision>(
                    Assert.Single(decisionsVm._solvedDecisionsSinceInit));
                Assert.Same(claimantDecision, preliminaryDecision.GetFollowUpDecision());

                // Not voted yet, the follow-up comes before the older ballots like in vanilla.
                Assert.Single(CaptureInquiries(decisionsVm.OnFrameTick)).AffirmativeAction();
                AssertOpenBallot(decisionsVm, claimantDecision);
                SubmitCurrentDecisionVote(decisionsVm, outcome =>
                    outcome is SettlementClaimantDecision.ClanAsDecisionOutcome clanOutcome && clanOutcome.Clan == Clan.PlayerClan);
                Assert.Null(decisionsVm.CurrentDecision);
                Assert.Contains(claimantDecision, Clan.PlayerClan.Kingdom.UnresolvedDecisions);

                // Once voted, the very next tick prompts the next ballot.
                Assert.Single(CaptureInquiries(decisionsVm.OnFrameTick)).AffirmativeAction();
                AssertOpenBallot(decisionsVm, warDecision);
                SubmitCurrentDecisionVote(decisionsVm, outcome => IsDeclareWarOutcome(outcome, true));

                // The fiefs tab Resolve still reopens it, and closing that view does not bring it back.
                decisionsVm.RefreshWith(claimantDecision);
                AssertClosableWaitingViewCloses(decisionsVm, claimantDecision);

                Assert.Single(CaptureInquiries(decisionsVm.OnFrameTick)).AffirmativeAction();
                AssertOpenBallot(decisionsVm, laterWarDecision);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }

        Assert.Equal(3, client1.NetworkSentMessages.GetMessages<NetworkRequestKingdomDecisionVote>().Count());
    }

    private static bool FixedClaimantMeritPrefix(ref float __result)
    {
        __result = 1f;
        return false;
    }

    [Fact]
    public void KingdomDecisionFinalVote_RemoteVotesKeepReopenedViewClosableUntilResolution()
    {
        var (client1, client2, _, player2, kingdomId) = SetUpPlayerDeclareWarVotes("TargetKingdomRemoteVote");
        KingdomDecisionsVM reopenedDecisionsVm = null;
        DecisionItemBaseVM reopenedDecisionItem = null;

        client1.Call(() =>
        {
            KingdomDecision decision = Assert.Single(Clan.PlayerClan.Kingdom.UnresolvedDecisions);
            SubmitDeclareWarVoteFromPanel(new KingdomDecisionsVM(() => { }), decision);
            reopenedDecisionsVm = new KingdomDecisionsVM(() => { });
            reopenedDecisionsVm.RefreshWith(decision);
            reopenedDecisionItem = reopenedDecisionsVm.CurrentDecision;

            Assert.True(reopenedDecisionItem.CanEndDecision);
        });

        client2.SimulateMessage(this, new KingdomDecisionVoteRequested(CreateDeclareWarNoVote(kingdomId, isFinal: false)));

        Assert.Contains(
            client1.InternalMessages.GetMessages<ApplyKingdomDecisionVote>(),
            message => message.ClanId == player2.ClanId && !message.VoteData.IsFinal);
        client1.Call(() =>
        {
            Assert.True(reopenedDecisionItem.IsActive);
            Assert.True(reopenedDecisionItem.CanEndDecision);
        });

        client2.SimulateMessage(this, new KingdomDecisionVoteRequested(CreateDeclareWarNoVote(kingdomId, isFinal: true)));

        Assert.Single(
            client1.InternalMessages.GetMessages<ApplyKingdomDecisionResolved>(),
            message => message.KingdomId == kingdomId);
        client1.Call(() =>
        {
            Assert.Null(reopenedDecisionsVm.CurrentDecision);
            Assert.False(reopenedDecisionItem.IsActive);
        });
    }

    [Fact]
    public void KingdomDecisionFinalVote_ResolutionAfterCloseAppliesWithoutPanel()
    {
        var (client1, client2, _, _, kingdomId) = SetUpPlayerDeclareWarVotes("TargetKingdomResolvedAfterClose");
        KingdomDecisionsVM decisionsVm = null;

        client1.Call(() =>
        {
            KingdomDecision decision = Assert.Single(Clan.PlayerClan.Kingdom.UnresolvedDecisions);
            decisionsVm = new KingdomDecisionsVM(() => { });
            SubmitDeclareWarVoteFromPanel(decisionsVm, decision);

            Assert.Null(decisionsVm.CurrentDecision);
        });

        client2.SimulateMessage(this, new KingdomDecisionVoteRequested(CreateDeclareWarVote(kingdomId, isFinal: true)));

        var resolvedMessage = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>(),
            message => message.KingdomId == kingdomId);
        Assert.Single(
            client1.InternalMessages.GetMessages<ApplyKingdomDecisionResolved>(),
            message => message.KingdomId == kingdomId);
        Assert.Contains(
            client1.InternalMessages.GetMessages<SendInformationMessage>(),
            message => message.Text == resolvedMessage.NotificationText);
        client1.Call(() =>
        {
            Assert.Null(decisionsVm.CurrentDecision);
            Assert.Single(decisionsVm._solvedDecisionsSinceInit);
        });
    }

    [Fact]
    public void KingdomDecisionSubmittedView_PendingRemoteVoteReplaysOnceWithoutRecursion()
    {
        var (client1, _, player1, player2, kingdomId) = SetUpPlayerDeclareWarVotes("TargetKingdomPendingReplay");

        client1.Call(() =>
        {
            KingdomDecision decision = Assert.Single(Clan.PlayerClan.Kingdom.UnresolvedDecisions);
            var decisionsVm = new KingdomDecisionsVM(() => { });
            decisionsVm.RefreshWith(decision);
            DecisionItemBaseVM decisionItem = decisionsVm.CurrentDecision;
            var voteManager = GetConcreteVoteManager(client1);

            // Reconnected mid-round: only the round status says the local clan already voted.
            voteManager.ApplyRoundStatus(CreateTwoClanRoundStatus(kingdomId, player1.ClanId, player2.ClanId, 60));
            Assert.True(decisionItem._finalSelectionDone);

            var pendingVotes = (System.Collections.IList)AccessTools
                .Field(typeof(KingdomDecisionVoteManager), "PendingRemoteVotes")
                .GetValue(voteManager);
            Type pendingVoteType = AccessTools.Inner(typeof(KingdomDecisionVoteManager), "PendingKingdomDecisionVote");
            pendingVotes.Add(Activator.CreateInstance(
                pendingVoteType,
                player2.ClanId,
                CreateDeclareWarNoVote(kingdomId, isFinal: false)));

            voteManager.ApplyRoundStatus(CreateTwoClanRoundStatus(kingdomId, player1.ClanId, player2.ClanId, 59));

            Assert.Empty(pendingVotes);
            Assert.True(decisionItem.IsActive);
            Assert.True(decisionItem.CanEndDecision);
            Assert.True(client1.ObjectManager.TryGetObject<Clan>(player2.ClanId, out var player2Clan));
            DecisionOutcome noOutcome = decisionItem.KingdomDecisionMaker._possibleOutcomes
                .Single(outcome => IsDeclareWarOutcome(outcome, false));
            Assert.Single(noOutcome.SupporterList, supporter => ReferenceEquals(supporter.Clan, player2Clan));
        });
    }

    [Fact]
    public void KingdomDecisionRoundStatus_RemapsLocalCandidateSetAndOrderToServerKeys()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomOutcomeOrder", _ => false);

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        KingdomDecisionRoundStatusData status = Server.NetworkSentMessages
            .GetMessages<NetworkKingdomDecisionRoundStatus>()
            .Last(message => message.Status.KingdomId == kingdomId)
            .Status;
        Assert.Equal(2, status.OrderedOutcomeKeys.Length);

        void AssertClientConverged(EnvironmentInstance client, bool dropFirstOutcome)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
                var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
                var decisionsVm = new KingdomDecisionsVM(() => { });
                decisionsVm.RefreshWith(decision);
                DecisionItemBaseVM decisionItem = decisionsVm.CurrentDecision;
                var resolver = client.Resolve<IKingdomDecisionOutcomeResolver>();

                List<DecisionOptionVM> nonAbstain = decisionItem.DecisionOptionsList
                    .Where(option => !option.IsOptionForAbstain)
                    .Reverse()
                    .ToList();
                DecisionOptionVM removedSelectedOption = null;
                if (dropFirstOutcome && nonAbstain.Count > 1)
                {
                    removedSelectedOption = nonAbstain[0];
                    removedSelectedOption.IsSelected = true;
                    decisionItem._currentSelectedOption = removedSelectedOption;
                    nonAbstain.RemoveAt(0);
                }

                decisionItem.DecisionOptionsList.Clear();
                var localOutcomes = new MBList<DecisionOutcome>();
                foreach (DecisionOptionVM option in nonAbstain)
                {
                    decisionItem.DecisionOptionsList.Add(option);
                    if (option.Option != null)
                    {
                        localOutcomes.Add(option.Option);
                    }
                }
                decisionItem.KingdomDecisionMaker._possibleOutcomes = localOutcomes;

                GetVoteManager(client).ApplyRoundStatus(status);

                string[] remappedKeys = decisionItem.DecisionOptionsList
                    .Where(option => !option.IsOptionForAbstain)
                    .Select(option =>
                    {
                        Assert.True(resolver.TryGetOutcomeKey(option.Option, client.ObjectManager, out string key));
                        Assert.Equal(
                            decision.CalculateMeritOfOutcome(option.Option),
                            option.Option.InitialMerit);
                        return key;
                    })
                    .ToArray();
                Assert.Equal(status.OrderedOutcomeKeys, remappedKeys);
                Assert.Equal(
                    status.OrderedOutcomeKeys,
                    client.Resolve<IKingdomDecisionOutcomeOrder>().CaptureKeys(
                        decisionItem.KingdomDecisionMaker._possibleOutcomes,
                        client.ObjectManager));
                if (removedSelectedOption != null)
                {
                    Assert.False(removedSelectedOption.IsSelected);
                    Assert.Null(decisionItem._currentSelectedOption);
                }
            });
        }

        AssertClientConverged(client1, dropFirstOutcome: true);
        AssertClientConverged(client2, dropFirstOutcome: false);
    }

    [Fact]
    public void ClearDecisionState_RemovesShiftedPendingRoundStatusesForKingdom()
    {
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        var player = CreateSyncedPlayerContext(ControllerId, client);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomClearedStatus", _ => false);

        ConfigureClanInKingdom(player.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        client.Call(() =>
        {
            var voteManager = GetVoteManager(client);
            voteManager.ApplyRoundStatus(new KingdomDecisionRoundStatusData(
                kingdomId,
                1,
                (DateTime.UtcNow + TimeSpan.FromSeconds(60)).Ticks,
                new[]
                {
                    new KingdomDecisionRoundClanStatusData(
                        player.ClanId,
                        player.ClanId,
                        ControllerId,
                        true,
                        true),
                }));
            voteManager.ClearDecisionState(kingdomId, 0);

            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(client.ObjectManager.TryGetObject<Clan>(player.ClanId, out var proposerClan));
            using (new AllowedThread())
            {
                kingdom._unresolvedDecisions ??= new MBList<KingdomDecision>();
                kingdom._unresolvedDecisions.Add(new DeclareWarDecision(proposerClan, targetKingdom));
                kingdom._unresolvedDecisions.Add(new DeclareWarDecision(proposerClan, targetKingdom));
            }

            var decision = Assert.IsType<DeclareWarDecision>(kingdom.UnresolvedDecisions[1]);
            voteManager.RegisterDecision(decision);

            Assert.False(voteManager.HasLocalPlayerSubmittedVote(decision));
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NpcPeaceOffer_TargetingPlayerKingdom_RequiresPlayerVote(bool acceptPeace)
    {
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        var player = CreateSyncedPlayerContext(ControllerId, client);
        var playerKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var enemyKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var enemyClanId = CreateSyncedNpcClan();

        ConfigureClanInKingdom(player.ClanId, playerKingdomId);
        ConfigureClanInKingdom(enemyClanId, enemyKingdomId);
        EnsureKingdomRegisteredEverywhere(playerKingdomId);
        EnsureKingdomRegisteredEverywhere(enemyKingdomId);
        ConfigureWarEverywhere(playerKingdomId, enemyKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(playerKingdomId, out var playerKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(enemyKingdomId, out var enemyKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(enemyClanId, out var enemyClan));

            var npcDecision = new MakePeaceKingdomDecision(
                enemyClan,
                playerKingdom,
                dailyTributeToBePaid: 100,
                dailyTributeDurationInDays: 30);
            var peaceOutcome = npcDecision.DetermineInitialCandidates()
                .OfType<MakePeaceKingdomDecision.MakePeaceDecisionOutcome>()
                .Single(outcome => outcome.ShouldPeaceBeDeclared);

            Assert.True(CoopKingdomElection.TryRedirectPlayerPeaceOffer(npcDecision, peaceOutcome));

            var playerDecision = Assert.IsType<MakePeaceKingdomDecision>(
                Assert.Single(playerKingdom.UnresolvedDecisions));
            Assert.Same(enemyKingdom, playerDecision.FactionToMakePeaceWith);
            Assert.Equal(-100, playerDecision.DailyTributeToBePaid);
            Assert.Equal(30, playerDecision.DailyTributeDurationInDays);
            Assert.True(playerDecision._isProposedByOpponent);
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkMakePeace>());
        Assert.Contains(
            Server.NetworkSentMessages.GetMessages<NetworkAddDecision>(),
            message => message.KingdomId == playerKingdomId
                       && message.Data is MakePeaceKingdomDecisionData peaceData
                       && peaceData.FactionToMakePeaceWithId == enemyKingdomId
                       && peaceData.DailyTributeToBePaid == -100
                       && peaceData.IsProposedByOpponent);

        foreach (var testClient in Clients)
        {
            testClient.Call(() =>
            {
                Assert.True(testClient.ObjectManager.TryGetObject<Kingdom>(playerKingdomId, out var playerKingdom));
                var playerDecision = Assert.IsType<MakePeaceKingdomDecision>(
                    Assert.Single(playerKingdom.UnresolvedDecisions));
                Assert.Equal(-100, playerDecision.DailyTributeToBePaid);
                Assert.True(playerDecision._isProposedByOpponent);
            });
        }

        client.SimulateMessage(
            this,
            new KingdomDecisionVoteRequested(CreateMakePeaceVote(playerKingdomId, acceptPeace)));

        if (acceptPeace)
        {
            Assert.Single(
                Server.NetworkSentMessages.GetMessages<NetworkMakePeace>(),
                message => message.Faction1Id == playerKingdomId
                           && message.Faction2Id == enemyKingdomId
                           && message.DailyTribute == -100
                           && message.DailyTributeDuration == 30);
        }
        else
        {
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkMakePeace>());
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(playerKingdomId, out var playerKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(enemyKingdomId, out var enemyKingdom));
            Assert.Empty(playerKingdom.UnresolvedDecisions);
            Assert.Equal(!acceptPeace, FactionManager.IsAtWarAgainstFaction(playerKingdom, enemyKingdom));
        });
    }

    [Fact]
    public void NpcPeaceOffer_TargetingPlayerKingdom_ExpiresAsDeclined()
    {
        var player = CreateSyncedPlayerContext(ControllerId, Clients.First());
        var playerKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var enemyKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var enemyClanId = CreateSyncedNpcClan();

        ConfigureClanInKingdom(player.ClanId, playerKingdomId);
        ConfigureClanInKingdom(enemyClanId, enemyKingdomId);
        EnsureKingdomRegisteredEverywhere(playerKingdomId);
        EnsureKingdomRegisteredEverywhere(enemyKingdomId);
        ConfigureWarEverywhere(playerKingdomId, enemyKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(playerKingdomId, out var playerKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(enemyKingdomId, out var enemyKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(enemyClanId, out var enemyClan));

            var npcDecision = new MakePeaceKingdomDecision(enemyClan, playerKingdom);
            var peaceOutcome = npcDecision.DetermineInitialCandidates()
                .OfType<MakePeaceKingdomDecision.MakePeaceDecisionOutcome>()
                .Single(outcome => outcome.ShouldPeaceBeDeclared);
            Assert.True(CoopKingdomElection.TryRedirectPlayerPeaceOffer(npcDecision, peaceOutcome));

            var playerDecision = Assert.IsType<MakePeaceKingdomDecision>(
                Assert.Single(playerKingdom.UnresolvedDecisions));
            playerDecision.TriggerTime = CampaignTime.Zero;

            CoopKingdomDecisionProposalBehaviorPatch.HourlyTickPrefix();

            Assert.Empty(playerKingdom.UnresolvedDecisions);
            Assert.True(FactionManager.IsAtWarAgainstFaction(playerKingdom, enemyKingdom));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkMakePeace>());
    }

    [Fact]
    public void AddDecision_UsesRegisteredPlayerClanWhenPartyClanPointsElsewhere()
    {
        var player = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var unrelatedClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var unrelatedKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player.ClanId, kingdomId);
        ConfigureClanInKingdom(unrelatedClanId, unrelatedKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);
        EnsureKingdomRegisteredEverywhere(unrelatedKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var playerClan));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(unrelatedClanId, out var unrelatedClan));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var playerParty));

            using (new AllowedThread())
            {
                playerParty.ActualClan = unrelatedClan;
            }

            kingdom.AddDecision(new DeclareWarDecision(playerClan, targetKingdom));

            Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>());
    }

    [Fact]
    public void KingdomDecisionVotes_MissingVoteTimesOutAfterVotingWindow()
    {
        var player = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomTimeout", _ => false);

        ConfigureClanInKingdom(player.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        DeclareWarDecision decision = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var playerClan));

            decision = new DeclareWarDecision(playerClan, targetKingdom);
            decision.TriggerTime = CampaignTime.Never;
            kingdom.AddDecision(decision);

            Assert.False(decision.ShouldBeCancelled());
            Assert.False(decision.TriggerTime.IsPast);
            CoopKingdomDecisionProposalBehaviorPatch.HourlyTickPrefix();

            Assert.Same(decision, Assert.Single(kingdom.UnresolvedDecisions));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));

            decision.TriggerTime = CampaignTime.Zero;
            Assert.True(decision.TriggerTime.IsPast);
            CoopKingdomDecisionProposalBehaviorPatch.HourlyTickPrefix();

            Assert.Same(decision, Assert.Single(kingdom.UnresolvedDecisions));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));

            GetConcreteVoteManager(Server).ProcessVotingRounds(
                DateTime.UtcNow + KingdomDecisionVoteManager.VotingRoundDuration);

            Assert.Empty(kingdom.UnresolvedDecisions);
        });

        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>(),
            message => message.KingdomId == kingdomId && message.DecisionIndex == 0);
    }

    [Fact]
    public void KingdomDecisionVotes_AllPlayersDisconnect_HourlySweepStillWaitsForDeadline()
    {
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        var player = CreateSyncedPlayerContext(ControllerId, client);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomDisconnectTimeout", _ => false);

        ConfigureClanInKingdom(player.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var playerClan));

            var decision = new DeclareWarDecision(playerClan, targetKingdom)
            {
                TriggerTime = CampaignTime.Zero,
            };
            kingdom.AddDecision(decision);
            Server.Resolve<IPlayerManager>().ClearPeer(client.NetPeer);

            Assert.True(decision.TriggerTime.IsPast);
            CoopKingdomDecisionProposalBehaviorPatch.HourlyTickPrefix();

            Assert.Same(decision, Assert.Single(kingdom.UnresolvedDecisions));
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());

            GetConcreteVoteManager(Server).ProcessVotingRounds(
                DateTime.UtcNow + KingdomDecisionVoteManager.VotingRoundDuration);

            Assert.Empty(kingdom.UnresolvedDecisions);
        });

        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>(),
            message => message.KingdomId == kingdomId && message.DecisionIndex == 0);
    }

    [Fact]
    public void KingdomDecisionVoteArrivingBeforeDecision_ReplaysWhenDecisionIsAdded()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomReplay", _ => false);

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        var player1NoVote = new KingdomDecisionVoteData(
            kingdomId,
            decisionIndex: 0,
            outcomeIndex: 1,
            supportWeight: (int)Supporter.SupportWeights.FullyPush,
            isAbstain: false,
            isFinal: true);
        client2.SimulateMessage(this, new NetworkChangeKingdomDecisionVote(player1.ClanId, player1NoVote));
        Assert.Contains(
            client2.InternalMessages.GetMessages<ApplyKingdomDecisionVote>(),
            message => message.ClanId == player1.ClanId
                       && message.VoteData.OutcomeIndex == 1
                       && message.VoteData.IsFinal);
        client2.Call(() =>
        {
            var voteManager = GetConcreteVoteManager(client2);
            var pendingVotes = (System.Collections.ICollection)AccessTools
                .Field(typeof(KingdomDecisionVoteManager), "PendingRemoteVotes")
                .GetValue(voteManager);

            Assert.Single(pendingVotes);
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        client2.Call(() =>
        {
            var voteManager = GetConcreteVoteManager(client2);
            Assert.True(client2.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            voteManager.RegisterDecision(decision);
            var states = (System.Collections.IDictionary)AccessTools
                .Field(typeof(KingdomDecisionVoteManager), "DecisionStates")
                .GetValue(voteManager);

            Assert.True(states.Contains(decision));
            object state = states[decision];
            var votes = (System.Collections.IDictionary)AccessTools
                .Property(state.GetType(), "Votes")
                .GetValue(state);
            var pendingVotes = (System.Collections.ICollection)AccessTools
                .Field(typeof(KingdomDecisionVoteManager), "PendingRemoteVotes")
                .GetValue(voteManager);

            object appliedVote = Assert.Single(votes.Values.Cast<object>());
            var voteData = (KingdomDecisionVoteData)AccessTools
                .Property(appliedVote.GetType(), "VoteData")
                .GetValue(appliedVote);

            Assert.Equal(1, voteData.OutcomeIndex);
            Assert.True(voteData.IsFinal);
            Assert.Empty(pendingVotes);

            var decisionsVm = new KingdomDecisionsVM(() => { });
            decisionsVm.RefreshWith(decision);
            var decisionItem = decisionsVm.CurrentDecision;
            var noOption = decisionItem.DecisionOptionsList.Single(option =>
                option.Option == decisionItem.KingdomDecisionMaker._possibleOutcomes[1]);
            Assert.True(client2.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var player1Clan));

            var supporter = Assert.Single(noOption.SupportersOfThisOption, supporterVm =>
                AccessTools.Field(typeof(DecisionSupporterVM), "_clan").GetValue(supporterVm) is Clan clan &&
                ReferenceEquals(clan, player1Clan));

            Assert.False(string.IsNullOrWhiteSpace(supporter.SupportWeightImagePath));
            Assert.NotNull(supporter.Visual);
        });
    }

    [Fact]
    public void KingdomDecisionVoteState_RemainsWithDecisionWhenEarlierDecisionIsRemoved()
    {
        var client1 = Clients.First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, Clients.Skip(1).First());
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var firstTargetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var secondTargetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(firstTargetKingdomId);
        EnsureKingdomRegisteredEverywhere(secondTargetKingdomId);

        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(firstTargetKingdomId, out var firstTargetKingdom));
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(secondTargetKingdomId, out var secondTargetKingdom));
            Assert.True(client1.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            using (new AllowedThread())
            {
                kingdom._unresolvedDecisions ??= new MBList<KingdomDecision>();
                kingdom._unresolvedDecisions.Add(new DeclareWarDecision(proposerClan, firstTargetKingdom));
                kingdom._unresolvedDecisions.Add(new DeclareWarDecision(proposerClan, secondTargetKingdom));
            }

            var firstDecision = Assert.IsType<DeclareWarDecision>(kingdom.UnresolvedDecisions[0]);
            var secondDecision = Assert.IsType<DeclareWarDecision>(kingdom.UnresolvedDecisions[1]);
            var voteManager = GetVoteManager(client1);
            voteManager.RegisterDecision(firstDecision);
            voteManager.RegisterDecision(secondDecision);

            var secondDecisionVote = new KingdomDecisionVoteData(
                kingdomId,
                decisionIndex: 1,
                outcomeIndex: 0,
                supportWeight: (int)Supporter.SupportWeights.FullyPush,
                isAbstain: false,
                isFinal: true);
            voteManager.ApplyRemoteVote(player1.ClanId, secondDecisionVote);
            Assert.True(voteManager.HasLocalPlayerSubmittedVote(secondDecision));

            using (new AllowedThread())
            {
                kingdom._unresolvedDecisions.RemoveAt(0);
            }

            var remainingDecision = Assert.Single(kingdom.UnresolvedDecisions);
            Assert.Same(secondDecision, remainingDecision);
            Assert.True(voteManager.HasLocalPlayerSubmittedVote(secondDecision));
            var debugInfo = Assert.Single(voteManager.GetDecisionDebugInfo(kingdom));
            Assert.Equal(0, debugInfo.DecisionIndex);
            Assert.Contains(debugInfo.ClientVotes, vote =>
                vote.ClanId == player1.ClanId &&
                vote.HasVote &&
                vote.IsFinal);
        });
    }

    [Fact]
    public void KingdomDecisionVoteState_RemainsWithNextDecisionWhenEarlierDecisionResolves()
    {
        var player1 = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var firstTargetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var secondTargetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(firstTargetKingdomId);
        EnsureKingdomRegisteredEverywhere(secondTargetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(firstTargetKingdomId, out var firstTargetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(secondTargetKingdomId, out var secondTargetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            using (new AllowedThread())
            {
                kingdom._unresolvedDecisions ??= new MBList<KingdomDecision>();
                kingdom._unresolvedDecisions.Add(new DeclareWarDecision(proposerClan, firstTargetKingdom));
                kingdom._unresolvedDecisions.Add(new DeclareWarDecision(proposerClan, secondTargetKingdom));
            }

            var firstDecision = Assert.IsType<DeclareWarDecision>(kingdom.UnresolvedDecisions[0]);
            var secondDecision = Assert.IsType<DeclareWarDecision>(kingdom.UnresolvedDecisions[1]);
            var voteManager = GetVoteManager(Server);
            voteManager.RegisterDecision(firstDecision);
            voteManager.RegisterDecision(secondDecision);

            var firstDecisionVote = CreateDeclareWarVote(kingdomId, isFinal: true);
            var secondDecisionVote = new KingdomDecisionVoteData(
                kingdomId,
                decisionIndex: 1,
                outcomeIndex: 0,
                supportWeight: (int)Supporter.SupportWeights.FullyPush,
                isAbstain: false,
                isFinal: true);
            voteManager.ApplyRemoteVote(player1.ClanId, firstDecisionVote);
            voteManager.ApplyRemoteVote(player1.ClanId, secondDecisionVote);
            var secondDecisionDebugInfo = Assert.Single(
                voteManager.GetDecisionDebugInfo(kingdom),
                info => info.DecisionIndex == 1);
            Assert.Contains(secondDecisionDebugInfo.ClientVotes, vote =>
                vote.ClanId == player1.ClanId &&
                vote.HasVote &&
                vote.IsFinal);

            Assert.True(voteManager.TryResolveDecision(firstDecision));

            var remainingDecision = Assert.Single(kingdom.UnresolvedDecisions);
            Assert.Same(secondDecision, remainingDecision);
            var debugInfo = Assert.Single(voteManager.GetDecisionDebugInfo(kingdom));
            Assert.Equal(0, debugInfo.DecisionIndex);
            Assert.Contains(debugInfo.ClientVotes, vote =>
                vote.ClanId == player1.ClanId &&
                vote.HasVote &&
                vote.IsFinal);
        });
    }

    [Fact]
    public void KingdomDecisionRemoval_ClearsServerVoteState()
    {
        var player = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var voteManager = GetConcreteVoteManager(Server);
            Assert.Single(voteManager.GetDecisionDebugInfo(kingdom));

            kingdom.RemoveDecision(decision);

            Assert.Empty(kingdom.UnresolvedDecisions);
            Assert.Empty(voteManager.GetDecisionDebugInfo(kingdom));
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
        });
    }

    [Fact]
    public void ProcessVotingRounds_DropsStaleRemovedDecisionWithoutResolving()
    {
        var player = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var voteManager = GetConcreteVoteManager(Server);
            var decisionStates = (System.Collections.IDictionary)AccessTools
                .Field(typeof(KingdomDecisionVoteManager), "DecisionStates")
                .GetValue(voteManager);

            Assert.True(decisionStates.Contains(decision));

            using (new AllowedThread())
            {
                kingdom._unresolvedDecisions.Remove(decision);
            }
            Assert.True(decisionStates.Contains(decision));

            voteManager.ProcessVotingRounds(DateTime.UtcNow + KingdomDecisionVoteManager.VotingRoundDuration);

            Assert.False(decisionStates.Contains(decision));
            Assert.Empty(voteManager.GetDecisionDebugInfo(kingdom));
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
        });
    }

    [Fact]
    public void KingdomDecisionPreview_DoesNotShowUnvotedPlayerClanAsSupporter()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomPreview", _ => false);

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client1.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var player1Clan));
            Assert.True(client1.ObjectManager.TryGetObject<Clan>(player2.ClanId, out var player2Clan));
            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var decisionsVm = new KingdomDecisionsVM(() => { });
            decisionsVm.RefreshWith(decision);
            var decisionItem = decisionsVm.CurrentDecision;
            var yesOption = decisionItem.DecisionOptionsList.Single(option =>
                IsDeclareWarOutcome(option.Option, true));

            var stalePlayerSupport = new Supporter(player2Clan);
            stalePlayerSupport.SupportWeight = Supporter.SupportWeights.FullyPush;
            yesOption.Option.AddSupport(stalePlayerSupport);
            Assert.Contains(yesOption.Option.SupporterList, supporter =>
                ReferenceEquals(supporter.Clan, player2Clan));

            var voteManager = GetVoteManager(client1);
            voteManager.UnregisterDecisionItem(decisionItem);
            voteManager.RegisterDecisionItem(decisionItem);

            Assert.DoesNotContain(yesOption.Option.SupporterList, supporter =>
                ReferenceEquals(supporter.Clan, player2Clan));
            Assert.DoesNotContain(yesOption.SupportersOfThisOption, supporterVm =>
                AccessTools.Field(typeof(DecisionSupporterVM), "_clan").GetValue(supporterVm) is Clan clan &&
                ReferenceEquals(clan, player2Clan));

            yesOption.CurrentSupportWeight = Supporter.SupportWeights.FullyPush;
            Assert.True(voteManager.TryPublishVote(yesOption));

            Assert.Contains(yesOption.SupportersOfThisOption, supporterVm =>
                AccessTools.Field(typeof(DecisionSupporterVM), "_clan").GetValue(supporterVm) is Clan clan &&
                ReferenceEquals(clan, player1Clan));
            Assert.DoesNotContain(yesOption.SupportersOfThisOption, supporterVm =>
                AccessTools.Field(typeof(DecisionSupporterVM), "_clan").GetValue(supporterVm) is Clan clan &&
                ReferenceEquals(clan, player2Clan));
        });
    }

    [Fact]
    public void KingdomDecisionResolveTabs_DiplomacyResolveReopensClosableWaitingViewAfterVote()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomResolveTabs", _ => false);

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        KingdomDecisionVoteData player1FinalVote = CreateDeclareWarVote(kingdomId, isFinal: true);

        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var voteManager = GetVoteManager(client1);
            voteManager.RegisterDecision(decision);
            voteManager.ApplyRemoteVote(player1.ClanId, player1FinalVote);
            Assert.True(voteManager.HasLocalPlayerSubmittedVote(decision));

            var decisionsVm = new KingdomDecisionsVM(() => { });
            KingdomDiplomacyProposalActionItemVM resolveAction =
                SelectDiplomacyResolveAction(kingdom, decision.FactionToDeclareWarOn, decisionsVm);
            Assert.True(resolveAction.IsEnabled);

            resolveAction.ExecuteAction();

            AssertClosableWaitingViewCloses(decisionsVm, decision);
        });

        client2.Call(() =>
        {
            Assert.True(client2.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var voteManager = GetVoteManager(client2);
            voteManager.RegisterDecision(decision);
            voteManager.ApplyRemoteVote(player1.ClanId, player1FinalVote);
            Assert.False(voteManager.HasLocalPlayerSubmittedVote(decision));

            var decisionsVm = new KingdomDecisionsVM(() => { });
            KingdomDiplomacyProposalActionItemVM resolveAction =
                SelectDiplomacyResolveAction(kingdom, decision.FactionToDeclareWarOn, decisionsVm);
            resolveAction.ExecuteAction();

            AssertOpenBallot(decisionsVm, decision);
        });

        Assert.Empty(client1.NetworkSentMessages.GetMessages<NetworkRequestKingdomDecisionVote>());
    }

    [Fact]
    public void KingdomDecisionResolveTabs_PolicyResolveReopensClosableWaitingViewAfterVote()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
                Assert.True(client.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

                PolicyObject policy = PolicyObject.All.First();
                Assert.NotNull(policy);
                using (new AllowedThread())
                {
                    kingdom._unresolvedDecisions ??= new MBList<KingdomDecision>();
                    kingdom._unresolvedDecisions.Add(new KingdomPolicyDecision(proposerClan, policy, false));
                }
            });
        }

        var player1FinalVote = new KingdomDecisionVoteData(
            kingdomId,
            decisionIndex: 0,
            outcomeIndex: 0,
            supportWeight: (int)Supporter.SupportWeights.FullyPush,
            isAbstain: false,
            isFinal: true);

        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var decision = Assert.IsType<KingdomPolicyDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var voteManager = GetVoteManager(client1);
            voteManager.RegisterDecision(decision);
            voteManager.ApplyRemoteVote(player1.ClanId, player1FinalVote);
            Assert.True(voteManager.HasLocalPlayerSubmittedVote(decision));

            var decisionsVm = new KingdomDecisionsVM(() => { });
            KingdomPoliciesVM policiesVm = SelectPolicyWithResolve(decision, decisionsVm);
            Assert.True(policiesVm.CanProposeOrDisavowPolicy);

            policiesVm.ExecuteProposeOrDisavow();

            AssertClosableWaitingViewCloses(decisionsVm, decision);
        });

        client2.Call(() =>
        {
            Assert.True(client2.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var decision = Assert.IsType<KingdomPolicyDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var voteManager = GetVoteManager(client2);
            voteManager.RegisterDecision(decision);
            voteManager.ApplyRemoteVote(player1.ClanId, player1FinalVote);
            Assert.False(voteManager.HasLocalPlayerSubmittedVote(decision));

            var decisionsVm = new KingdomDecisionsVM(() => { });
            KingdomPoliciesVM policiesVm = SelectPolicyWithResolve(decision, decisionsVm);
            Assert.True(policiesVm.CanProposeOrDisavowPolicy);

            policiesVm.ExecuteProposeOrDisavow();

            AssertOpenBallot(decisionsVm, decision);
        });

        Assert.Empty(client1.NetworkSentMessages.GetMessages<NetworkRequestKingdomDecisionVote>());
    }

    [Fact]
    public void KingdomDecisionVoteBroadcast_ReplaysRemoteNoVoteInDecisionUi()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomBroadcast", _ => false);

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        KingdomDecisionVoteData player1NoVote = CreateDeclareWarVoteFromUi(client1, shouldWarBeDeclared: false);
        Assert.Equal((int)Supporter.SupportWeights.FullyPush, player1NoVote.SupportWeight);

        client1.SimulateMessage(this, new KingdomDecisionVoteRequested(player1NoVote));

        var broadcastVote = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkChangeKingdomDecisionVote>(),
            message => message.VoteData.KingdomId == kingdomId
                       && message.VoteData.DecisionIndex == 0
                       && message.VoteData.OutcomeIndex == 1
                       && message.VoteData.OutcomeKey == player1NoVote.OutcomeKey
                       && message.VoteData.IsFinal);
        Assert.Equal(player1.ClanId, broadcastVote.ClanId);
        Assert.Equal((int)Supporter.SupportWeights.FullyPush, broadcastVote.VoteData.SupportWeight);
        Assert.False(string.IsNullOrWhiteSpace(broadcastVote.VoteData.OutcomeKey));

        client2.SimulateMessage(this, broadcastVote);

        client2.Call(() =>
        {
            Assert.True(client2.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var decisionsVm = new KingdomDecisionsVM(() => { });
            decisionsVm.RefreshWith(decision);
            var decisionItem = decisionsVm.CurrentDecision;
            var yesOption = decisionItem.DecisionOptionsList.Single(option =>
                IsDeclareWarOutcome(option.Option, true));
            var noOption = decisionItem.DecisionOptionsList.Single(option =>
                IsDeclareWarOutcome(option.Option, false));

            Assert.True(client2.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var player1Clan));

            var voteManager = GetVoteManager(client2);
            voteManager.UnregisterDecisionItem(decisionItem);
            voteManager.RegisterDecisionItem(decisionItem);

            Assert.Equal(0, yesOption.WinPercentage);
            Assert.Equal(100, noOption.WinPercentage);
            Assert.DoesNotContain(yesOption.SupportersOfThisOption, supporterVm =>
                AccessTools.Field(typeof(DecisionSupporterVM), "_clan").GetValue(supporterVm) is Clan clan &&
                ReferenceEquals(clan, player1Clan));

            var supporter = Assert.Single(noOption.SupportersOfThisOption, supporterVm =>
                AccessTools.Field(typeof(DecisionSupporterVM), "_clan").GetValue(supporterVm) is Clan clan &&
                ReferenceEquals(clan, player1Clan));

            Assert.False(string.IsNullOrWhiteSpace(supporter.SupportWeightImagePath));
            Assert.NotNull(supporter.Visual);
        });
    }

    [Fact]
    public void KingdomDecisionRefreshWinPercentages_NoSponsors_NormalizesOptions()
    {
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        var player = CreateSyncedPlayerContext(ControllerId, client);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var election = new KingdomElection(decision);
            var decisionItem = ObjectHelper.SkipConstructor<DecisionItemBaseVM>();
            decisionItem.DecisionOptionsList = new MBBindingList<DecisionOptionVM>();
            decisionItem.KingdomDecisionMaker = election;

            foreach (DecisionOutcome outcome in election._possibleOutcomes)
            {
                outcome.SupporterList.Clear();

                var option = ObjectHelper.SkipConstructor<DecisionOptionVM>();
                option.Option = outcome;
                decisionItem.DecisionOptionsList.Add(option);
            }

            decisionItem.RefreshWinPercentages();

            Assert.Collection(
                decisionItem.DecisionOptionsList,
                option =>
                {
                    Assert.Null(option.Sponsor);
                    Assert.Equal(50, option.WinPercentage);
                },
                option =>
                {
                    Assert.Null(option.Sponsor);
                    Assert.Equal(50, option.WinPercentage);
                });
            Assert.Equal(100, decisionItem.DecisionOptionsList.Sum(option => option.WinPercentage));
        });
    }

    [Fact]
    public void KingdomDecisionDebugCommand_ListsClientVoteState()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomDebugCommand", _ => false);

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        KingdomDecisionVoteData player1NoVote = CreateDeclareWarVoteFromUi(client1, shouldWarBeDeclared: false);
        Assert.Equal((int)Supporter.SupportWeights.FullyPush, player1NoVote.SupportWeight);
        client1.SimulateMessage(this, new KingdomDecisionVoteRequested(player1NoVote));

        var broadcastVote = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkChangeKingdomDecisionVote>(),
            message => message.VoteData.KingdomId == kingdomId
                       && message.VoteData.DecisionIndex == 0
                       && message.VoteData.OutcomeIndex == 1
                       && message.VoteData.OutcomeKey == player1NoVote.OutcomeKey
                       && message.VoteData.IsFinal);
        Assert.Equal((int)Supporter.SupportWeights.FullyPush, broadcastVote.VoteData.SupportWeight);
        client2.SimulateMessage(this, broadcastVote);

        string output = null;
        client2.Call(() =>
        {
            output = ExecuteCommand(
                new KingdomDebugCommand.KingdomDecisionsCoopCommand(),
                kingdomId);
        });

        Assert.Contains("DeclareWarDecision", output);
        Assert.Contains(ControllerId, output);
        Assert.Contains("Voted No", output);
        Assert.Contains("Support: FullyPush", output);
        Assert.Contains(SecondControllerId, output);
        Assert.Contains("Not Voted", output);
        Assert.DoesNotContain("Voted Yes", output);
    }

    [Fact]
    public void ClientKingdomDecisionProposal_AddsPendingDecisionOnServerAndClients()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(client1.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        var addDecisionMessage = Assert.Single(client1.NetworkSentMessages.GetMessages<NetworkAddDecision>());
        Assert.Equal(kingdomId, addDecisionMessage.KingdomId);
        Assert.Equal(kingdomId, addDecisionMessage.Data.KingdomId);
        Assert.Equal(player1.ClanId, addDecisionMessage.Data.ProposerClanId);
        Assert.IsType<DeclareWarDecisionData>(addDecisionMessage.Data);
        Assert.Contains(Server.InternalMessages.GetMessages<NetworkAddDecision>(), message => message.KingdomId == kingdomId);
        Assert.Contains(Server.InternalMessages.GetMessages<AddDecision>(), message => message.KingdomId == kingdomId);
        Assert.DoesNotContain(client1.InternalMessages.GetMessages<AddDecision>(), message => message.KingdomId == kingdomId);
        Assert.Contains(client2.InternalMessages.GetMessages<AddDecision>(), message => message.KingdomId == kingdomId);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.Single(kingdom.UnresolvedDecisions);
            Assert.IsType<DeclareWarDecision>(kingdom.UnresolvedDecisions[0]);
        });

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
                Assert.Single(kingdom.UnresolvedDecisions);
                Assert.IsType<DeclareWarDecision>(kingdom.UnresolvedDecisions[0]);
            });
        }

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>());
    }

    [Fact]
    public void ClientKingdomDecisionProposal_SpendsInfluenceOnlyOnServer()
    {
        var client1 = Clients.First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        const float initialInfluence = 500f;

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));
                using (new AllowedThread())
                {
                    proposerClan._influence = initialInfluence;
                }
            });
        }

        int influenceCost = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            influenceCost = new DeclareWarDecision(proposerClan, targetKingdom).GetInfluenceCost(proposerClan);
            Assert.True(influenceCost > 0);
        });

        KingdomDecisionData decisionData = null;
        float clientInfluenceAfterProposal = 0;
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(client1.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            var decision = new DeclareWarDecision(proposerClan, targetKingdom);
            kingdom.AddDecision(decision);
            decisionData = client1.Resolve<IKingdomDecisionDataConverter>().Convert(decision);
            clientInfluenceAfterProposal = proposerClan.Influence;
        }, new[] { AccessTools.Method(typeof(ClientKingdomHandler), "HandleLocalDecisionAdded") });

        Assert.Equal(initialInfluence, clientInfluenceAfterProposal, precision: 4);
        Assert.NotNull(decisionData);
        Server.SimulateMessage(
            this,
            new NetworkAddDecision(kingdomId, decisionData, ignoreInfluenceCost: false, randomNumber: 0.5f));
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));
            Assert.Equal(initialInfluence - influenceCost, proposerClan.Influence, precision: 4);
        });
    }

    [Fact]
    public void KingdomDecisionVotes_RejectVotesWithoutCurrentLeaderHeroMapping()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));
            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.TryGetPlayer(SecondControllerId, out var registeredPlayer));
            Assert.True(playerManager.ReplacePlayer(
                registeredPlayer,
                new Player(
                    SecondControllerId,
                    "missingHero",
                    player2.PartyId,
                    player2.ClanId,
                    player2.CharacterId)));
        });

        client1.SimulateMessage(this, new KingdomDecisionVoteRequested(CreateDeclareWarVote(kingdomId, isFinal: true)));

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.Single(kingdom.UnresolvedDecisions);
        });

        client2.SimulateMessage(this, new KingdomDecisionVoteRequested(CreateDeclareWarVote(kingdomId, isFinal: true)));

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.TryGetPlayer(SecondControllerId, out var registeredPlayer));
            Assert.True(playerManager.ReplacePlayer(registeredPlayer, new Player(
                SecondControllerId, player2.HeroId, player2.PartyId, player2.ClanId, player2.CharacterId)));
        });
        client2.SimulateMessage(this, new KingdomDecisionVoteRequested(CreateDeclareWarVote(kingdomId, isFinal: true)));

        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>(),
            message => message.KingdomId == kingdomId
                       && message.DecisionIndex == 0
                       && message.OutcomeIndex == 0
                       && message.IsPlayerDecision);
        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>(),
            message => message.Faction1Id == kingdomId
                       && message.Faction2Id == targetKingdomId
                       && message.Detail == (int)DeclareWarAction.DeclareWarDetail.CausedByKingdomDecision);
    }

    [Fact]
    public void KingdomDecisionVoteData_CreatedFromDecisionVmUsesRegisteredKingdomId()
    {
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        var player = CreateSyncedPlayerContext(ControllerId, client);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);
        SetKingdomStringIdEverywhere(kingdomId, "native_created_kingdom");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        KingdomDecisionVoteData voteData = null;
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var election = new KingdomElection(decision);
            election.Setup();
            election.DetermineSupport(election._possibleOutcomes, false);
            decision.DetermineSponsors(election._possibleOutcomes);
            election.UpdateSupport(election._possibleOutcomes);

            var option = ObjectHelper.SkipConstructor<DecisionOptionVM>();
            AccessTools.Field(typeof(DecisionOptionVM), "<Option>k__BackingField")
                .SetValue(option, election._possibleOutcomes[0]);
            AccessTools.Field(typeof(DecisionOptionVM), "<Decision>k__BackingField")
                .SetValue(option, decision);
            AccessTools.Field(typeof(DecisionOptionVM), "_kingdomDecisionMaker")
                .SetValue(option, election);
            option.CurrentSupportWeight = Supporter.SupportWeights.FullyPush;

            Assert.Same(decision, option.Decision);
            Assert.Same(election._possibleOutcomes[0], option.Option);
            Assert.Same(kingdom, Clan.PlayerClan.Kingdom);
            Assert.Same(Hero.MainHero, Clan.PlayerClan.Leader);
            Assert.Contains(option.Option, election._possibleOutcomes);

            Assert.True(GetVoteManager(client).TryCreateVoteData(option, out voteData, isFinal: true));
        });

        Assert.Equal(kingdomId, voteData.KingdomId);
        Assert.NotEqual("native_created_kingdom", voteData.KingdomId);

        client.SimulateMessage(this, new KingdomDecisionVoteRequested(voteData));

        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>(),
            message => message.KingdomId == kingdomId
                       && message.DecisionIndex == 0
                       && message.OutcomeIndex == 0
                       && message.IsPlayerDecision);
    }

    [Fact]
    public void KingdomDecisionFinalVote_MissingKingdomId_KeepsDecisionUiOpenAndRetries()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        DecisionItemBaseVM submittedDecisionItem = null;
        KingdomDecisionsVM retryDecisionsVm = null;
        DecisionItemBaseVM retryDecisionItem = null;
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);
        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);

        string policyId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));
            PolicyObject policy = PolicyObject.All.First(candidate => !kingdom.ActivePolicies.Contains(candidate));
            policyId = policy.StringId;
            using (new AllowedThread())
            {
                kingdom._unresolvedDecisions.Add(new KingdomPolicyDecision(proposerClan, policy, false));
            }
        });

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
                Assert.True(client.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));
                PolicyObject policy = PolicyObject.All.Single(candidate => candidate.StringId == policyId);
                using (new AllowedThread())
                {
                    kingdom._unresolvedDecisions.Add(new KingdomPolicyDecision(proposerClan, policy, false));
                }
            });
        }

        client2.Call(() =>
        {
            Assert.True(client2.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var decision = Assert.IsType<KingdomPolicyDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var decisionsVm = new KingdomDecisionsVM(() => { });
            decisionsVm.RefreshWith(decision);

            var decisionItem = Assert.IsType<PolicyDecisionItemVM>(decisionsVm.CurrentDecision);
            submittedDecisionItem = decisionItem;
            DecisionOptionVM option = decisionItem.DecisionOptionsList.Single(candidate =>
                candidate.Option is KingdomPolicyDecision.PolicyDecisionOutcome outcome &&
                outcome.ShouldDecisionBeEnforced);
            option.CurrentSupportWeight = Supporter.SupportWeights.FullyPush;
            decisionItem._currentSelectedOption = option;
            string decisionDescription = decisionItem.DescriptionText;
            decisionItem.ExecuteFinalSelection();

            Assert.Null(decisionsVm.CurrentDecision);
            Assert.False(decisionItem.IsActive);
            Assert.True(decisionItem._finalSelectionDone);
            Assert.Equal(decisionDescription, decisionItem.DescriptionText);
        });

        client2.Call(() =>
            Assert.Contains(
                "Vote submitted",
                GetVoteManager(client2).RefreshDecisionWaitingStatus(submittedDecisionItem)));

        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var decision = Assert.IsType<KingdomPolicyDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var decisionsVm = new KingdomDecisionsVM(() => { });
            decisionsVm.RefreshWith(decision);

            var decisionItem = Assert.IsType<PolicyDecisionItemVM>(decisionsVm.CurrentDecision);
            retryDecisionsVm = decisionsVm;
            retryDecisionItem = decisionItem;
            DecisionOptionVM option = decisionItem.DecisionOptionsList.Single(candidate =>
                candidate.Option is KingdomPolicyDecision.PolicyDecisionOutcome outcome &&
                outcome.ShouldDecisionBeEnforced);
            option.CurrentSupportWeight = Supporter.SupportWeights.FullyPush;
            decisionItem._currentSelectedOption = option;
            RemoveReverseObjectManagerId(client1, kingdom);

            decisionItem.ExecuteFinalSelection();

            Assert.Same(decisionItem, decisionsVm.CurrentDecision);
            Assert.True(decisionItem.IsActive);
            Assert.False(decisionItem._finalSelectionDone);
            Assert.Empty(client1.NetworkSentMessages.GetMessages<NetworkRequestKingdomDecisionVote>());

            RestoreReverseObjectManagerId(client1, kingdom, kingdomId);
            decisionItem.ExecuteFinalSelection();
        });

        client1.Call(() =>
        {
            Assert.Null(retryDecisionsVm.CurrentDecision);
            Assert.False(retryDecisionItem.IsActive);
        });

        Assert.Single(client1.NetworkSentMessages.GetMessages<NetworkRequestKingdomDecisionVote>());
        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>(),
            message => message.KingdomId == kingdomId);
    }

    // A war against a kingdom that has an ally makes the alliance behaviour propose a call to war in
    // that kingdom. The server replicates the war to every client, and each client replays the war
    // action locally.
    [Fact]
    public void KingdomDecisionAdded_WarDeclaredOnKingdomWithAlly_ClientHoldsOnlyServerDecisions()
    {
        var scenario = SetUpCallToWarScenario("Twin");
        try
        {
            DeclareWarOnServer(scenario.KingdomId, scenario.EnemyId);

            int serverDecisionCount = CountDecisions(Server, scenario.KingdomId);
            int serverAddCount = Server.NetworkSentMessages.GetMessages<NetworkAddDecision>()
                .Count(message => message.KingdomId == scenario.KingdomId);
            Assert.Equal(1, serverDecisionCount);
            foreach (var client in Clients)
            {
                int clientDecisionCount = CountDecisions(client, scenario.KingdomId);
                Assert.True(
                    clientDecisionCount == serverDecisionCount,
                    $"A client holds {clientDecisionCount} decision(s) after the war declaration; the server holds " +
                    $"{serverDecisionCount} and sent {serverAddCount} of them. " +
                    $"Client decisions: {DescribeDecisionTypes(client, scenario.KingdomId)}.");
            }
        }
        finally
        {
            RemoveAllianceWarListeners(scenario);
        }
    }

    // Control: the same war with the alliance behaviour answering it on the server only.
    [Fact]
    public void KingdomDecisionAdded_WarDeclaredOnKingdomWithAllyHandledOnServerOnly_ClientHoldsOnlyServerDecisions()
    {
        var scenario = SetUpCallToWarScenario("NoTwin", clientsHandleWar: false);
        try
        {
            DeclareWarOnServer(scenario.KingdomId, scenario.EnemyId);

            Assert.Equal(1, CountDecisions(Server, scenario.KingdomId));
            foreach (var client in Clients)
            {
                Assert.Equal(1, CountDecisions(client, scenario.KingdomId));
            }
        }
        finally
        {
            RemoveAllianceWarListeners(scenario);
        }
    }

    [Fact]
    public void KingdomDecisionFinalVote_WarDeclaredOnKingdomWithAlly_ServerCountsVote()
    {
        var scenario = SetUpCallToWarScenario("TwinVote");
        try
        {
            DeclareWarOnServer(scenario.KingdomId, scenario.EnemyId);

            scenario.Client.Call(() =>
            {
                Assert.True(scenario.Client.ObjectManager.TryGetObject<Kingdom>(scenario.KingdomId, out var kingdom));
                // The decision the server sent arrives after the replayed war, so it is the last entry.
                KingdomDecision serverDecision = kingdom.UnresolvedDecisions.Last();
                // The decision screen needs hero data the test campaign does not have, so the ballot goes
                // through the election route, which finds the decision's list position the same way.
                var election = new KingdomElection(serverDecision);
                election._chosenOutcome = election._possibleOutcomes.Single(candidate =>
                    candidate is ProposeCallToWarAgreementDecision.ProposeCallToWarAgreementDecisionOutcome outcome &&
                    outcome.ShouldCallToWar);
                Assert.True(GetVoteManager(scenario.Client).TryPublishFinalVoteForElection(election));
            });

            NetworkRequestKingdomDecisionVote sentVote = Assert.Single(
                scenario.Client.NetworkSentMessages.GetMessages<NetworkRequestKingdomDecisionVote>(),
                message => message.VoteData.IsFinal);
            int serverDecisionCount = CountDecisions(Server, scenario.KingdomId);

            Assert.True(
                Server.NetworkSentMessages.GetMessages<NetworkChangeKingdomDecisionVote>()
                    .Any(message => message.ClanId == scenario.LocalClanId && message.VoteData.IsFinal),
                $"The server did not count the final vote. The client sent it for decision position " +
                $"{sentVote.VoteData.DecisionIndex}; the server holds {serverDecisionCount} decision(s).");
        }
        finally
        {
            RemoveAllianceWarListeners(scenario);
        }
    }

    private CallToWarScenario SetUpCallToWarScenario(string name, bool clientsHandleWar = true)
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var allyId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var enemyId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var allyPlayer = CreateSyncedPlayerContext($"{name}Ally", _ => false);
        var enemyPlayer = CreateSyncedPlayerContext($"{name}Enemy", _ => false);

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        ConfigureClanInKingdom(allyPlayer.ClanId, allyId);
        ConfigureClanInKingdom(enemyPlayer.ClanId, enemyId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(allyId);
        EnsureKingdomRegisteredEverywhere(enemyId);

        var behaviors = new List<(EnvironmentInstance Instance, AllianceCampaignBehavior Behavior)>();
        IEnumerable<EnvironmentInstance> listeningInstances = clientsHandleWar ? new[] { Server }.Concat(Clients) : new[] { Server };
        foreach (var instance in new[] { Server }.Concat(Clients))
        {
            bool listens = listeningInstances.Contains(instance);
            AllianceCampaignBehavior behavior = null;
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
                Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(allyId, out var ally));
                using (new AllowedThread())
                {
                    kingdom._alliedKingdoms ??= new MBList<Kingdom>();
                    ally._alliedKingdoms ??= new MBList<Kingdom>();
                    kingdom._alliedKingdoms.Add(ally);
                    ally._alliedKingdoms.Add(kingdom);
                }

                if (!listens) return;

                // The game registers this listener through the alliance behaviour of each campaign.
                behavior = new AllianceCampaignBehavior();
                CampaignEvents.WarDeclared.AddNonSerializedListener(behavior, behavior.OnWarDeclared);
            });
            if (behavior != null) behaviors.Add((instance, behavior));
        }

        return new CallToWarScenario(client1, kingdomId, enemyId, player1.ClanId, behaviors);
    }

    private void DeclareWarOnServer(string kingdomId, string enemyId)
    {
        // The call-to-war price needs campaign statistics the test campaign does not have, and the
        // price plays no part in these tests, so it is left at zero on every side.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(enemyId, out var enemy));
            DeclareWarAction.ApplyByDefault(enemy, kingdom);
        }, new[] { AccessTools.Method(typeof(DefaultAllianceModel), nameof(DefaultAllianceModel.GetCallToWarCost)) });

        Assert.Contains(
            Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>(),
            message => message.Faction1Id == enemyId && message.Faction2Id == kingdomId);
    }

    private static void RemoveAllianceWarListeners(CallToWarScenario scenario)
    {
        foreach (var (instance, behavior) in scenario.Behaviors)
        {
            instance.Call(() => CampaignEvents.WarDeclared.ClearListeners(behavior));
        }
    }

    private static string DescribeDecisionTypes(EnvironmentInstance instance, string kingdomId)
    {
        string description = null;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            description = kingdom.UnresolvedDecisions.Count == 0
                ? "none"
                : string.Join(", ", kingdom.UnresolvedDecisions.Select(decision => decision.GetType().Name));
        });
        return description;
    }

    private sealed record CallToWarScenario(
        EnvironmentInstance Client,
        string KingdomId,
        string EnemyId,
        string LocalClanId,
        List<(EnvironmentInstance Instance, AllianceCampaignBehavior Behavior)> Behaviors);

    private static int CountDecisions(EnvironmentInstance instance, string kingdomId)
    {
        int count = 0;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            count = kingdom.UnresolvedDecisions.Count;
        });
        return count;
    }

    [Fact]
    public void PlayerKingdomCreatedNotification_RelinksClientClanWhenFieldSyncHasNotArrived()
    {
        var player = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var client = Clients.First();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));

            using (new AllowedThread())
            {
                kingdom._rulingClan = null;
                clan.Kingdom = null;
            }
        });

        client.SimulateMessage(
            this,
            new NetworkPlayerKingdomCreated(ControllerId, kingdomId, KingdomName, player.ClanId));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));

            Assert.Same(kingdom, clan.Kingdom);
            Assert.Same(clan, kingdom.RulingClan);
            AssertKingdomReadyForManagementUi(kingdom, clan);
            Assert.Contains(kingdom, Campaign.Current.CampaignObjectManager.Kingdoms);
        });
    }

    [Fact]
    public void PlayerKingdomCreatedNotification_InitializesClientKingdomManagementState()
    {
        var player = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var client = Clients.First();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));

            using (new AllowedThread())
            {
                kingdom._activePolicies = null;
                kingdom._armies = null;
                kingdom._clans = null;
                kingdom._unresolvedDecisions = null;
                kingdom._rulingClan = null;
                clan.Kingdom = null;
            }
        });

        client.SimulateMessage(
            this,
            new NetworkPlayerKingdomCreated(ControllerId, kingdomId, KingdomName, player.ClanId));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));

            Assert.Same(kingdom, clan.Kingdom);
            Assert.Same(clan, kingdom.RulingClan);
            AssertKingdomReadyForManagementUi(kingdom, clan);
        });
    }

    [Fact]
    public void ClientKingdomCreationRequest_PreservesSettlementContextWhenNotificationReturnsImmediately()
    {
        var player = CreateSyncedPlayerContext();
        var client = Clients.First();
        var settlementId = CreateSyncedSettlement();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }

            Assert.Same(settlement, party.CurrentSettlement);
        });

        client.SimulateMessage(this, new KingdomCreationRequested(KingdomName, player.CultureId));

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestCreateKingdom>());
        Assert.Equal(ControllerId, request.ControllerId);
        Assert.Equal(player.PartyId, request.PartyId);
        Assert.Equal(settlementId, request.SettlementId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Same(settlement, party.CurrentSettlement);
        });

        Assert.Contains(
            client.InternalMessages.GetMessages<PlayerKingdomCreated>(),
            message => message.ControllerId == ControllerId
                       && message.ClanId == player.ClanId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Same(settlement, party.CurrentSettlement);
        });

        Server.PumpGameThread();
    }

    [Fact]
    public void ClientKingdomCreationRequest_DoesNotUseSettlementStringIdWhenReverseLookupIsMissing()
    {
        var player = CreateSyncedPlayerContext();
        var client = Clients.First();
        var settlementId = CreateSyncedSettlement();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }

            RemoveReverseObjectManagerId(client, settlement);

            Assert.False(client.ObjectManager.TryGetId(settlement, out _));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var _));
            Assert.Same(settlement, party.CurrentSettlement);
        });

        client.SimulateMessage(this, new KingdomCreationRequested(KingdomName, player.CultureId));

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestCreateKingdom>());
        Assert.Equal(ControllerId, request.ControllerId);
        Assert.Null(request.PartyId);
        Assert.Null(request.SettlementId);

        Server.PumpGameThread();
    }

    [Fact]
    public void PlayerKingdomCreatedNotification_RestoresCreatingClientSettlementContextAfterLocalExit()
    {
        var player = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var client = Clients.First();
        var settlementId = CreateSyncedSettlement();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }

            Assert.Same(settlement, party.CurrentSettlement);
        });

        var handler = client.Resolve<ClientKingdomHandler>();
        var pending = AccessTools.Method(typeof(ClientKingdomHandler), "CapturePendingSettlementRestore")
            .Invoke(handler, Array.Empty<object>());
        Assert.NotNull(pending);
        AccessTools.Field(typeof(ClientKingdomHandler), "pendingKingdomCreationSettlement")
            .SetValue(handler, pending);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                party.CurrentSettlement = null;
            }

            Assert.Same(settlement, party.CurrentSettlement);
        });

        client.SimulateMessage(
            this,
            new NetworkPlayerKingdomCreated(ControllerId, kingdomId, KingdomName, player.ClanId, player.PartyId, settlementId));

        Assert.Contains(
            client.InternalMessages.GetMessages<StartSettlementEncounterAttempted>(),
            message => ReferenceEquals(message.Party, GetObject<MobileParty>(client, player.PartyId))
                       && ReferenceEquals(message.Settlement, GetObject<Settlement>(client, settlementId)));

        Assert.Contains(
            client.NetworkSentMessages.GetMessages<NetworkRequestStartSettlementEncounter>(),
            message => message.PartyId == client.GetHandle<MobileParty>(player.PartyId) &&
                       message.SettlementId == client.GetHandle<Settlement>(settlementId));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Same(settlement, party.CurrentSettlement);
        });
    }

    [Fact]
    public void PlayerKingdomCreatedNotification_RestoresSettlementWithoutPublishingLeave()
    {
        var player = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var client = Clients.First();
        var settlementId = CreateSyncedSettlement();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        TestEnvironment.ConnectRegisteredPlayer(client, ControllerId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }

            Assert.Same(settlement, party.CurrentSettlement);
        });

        var handler = client.Resolve<ClientKingdomHandler>();
        var pending = AccessTools.Method(typeof(ClientKingdomHandler), "CapturePendingSettlementRestore")
            .Invoke(handler, Array.Empty<object>());
        Assert.NotNull(pending);
        AccessTools.Field(typeof(ClientKingdomHandler), "pendingKingdomCreationSettlement")
            .SetValue(handler, pending);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                party.CurrentSettlement = null;
            }

            Assert.Same(settlement, party.CurrentSettlement);
        });

        client.SimulateMessage(
            this,
            new NetworkPlayerKingdomCreated(ControllerId, kingdomId, KingdomName, player.ClanId, player.PartyId, settlementId));

        Assert.DoesNotContain(
            client.NetworkSentMessages.GetMessages<NetworkRequestEndSettlementEncounter>(),
            message => message.PartyId == client.GetHandle<MobileParty>(player.PartyId));
        Assert.DoesNotContain(
            Server.NetworkSentMessages.GetMessages<NetworkPartyLeaveSettlement>(),
            message => message.PartyId == Server.GetHandle<MobileParty>(player.PartyId));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Same(settlement, party.CurrentSettlement);
        });
        AssertCompletedSettlementProtectionDisarms(client, player.PartyId, settlementId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Same(settlement, party.CurrentSettlement);
        });
    }

    [Fact]
    public void PlayerKingdomCreatedNotification_RemoteClientAllowsLaterSettlementLeave()
    {
        var player = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var client = Clients.Skip(1).First();
        var settlementId = CreateSyncedSettlement();
        client.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Null(party.CurrentSettlement);
            Assert.NotNull(settlement);
        });

        client.SimulateMessage(
            this,
            new NetworkPlayerKingdomCreated(ControllerId, kingdomId, KingdomName, player.ClanId, player.PartyId, settlementId));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Same(settlement, party.CurrentSettlement);
        });

        client.SimulateMessage(this, new NetworkPartyEnterSettlement(
            client.GetHandle<Settlement>(settlementId),
            client.GetHandle<MobileParty>(player.PartyId)));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));

            using (new AllowedThread())
            {
                party.CurrentSettlement = null;
            }

            Assert.Null(party.CurrentSettlement);
        });
    }

    [Fact]
    public void ClientKingdomCreationRequest_ServerSuppressesAccidentalSettlementLeaveConsequence()
    {
        var player = CreateSyncedPlayerContext();
        var client = Clients.First();
        var settlementId = CreateSyncedSettlement();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }
        });

        client.SimulateMessage(this, new KingdomCreationRequested(KingdomName, player.CultureId));
        client.SimulateMessage(this, new EndSettlementEncounterAttempted(GetObject<MobileParty>(client, player.PartyId)));

        var leaveRequest = Assert.Single(
            client.NetworkSentMessages.GetMessages<NetworkRequestEndSettlementEncounter>(),
            message => message.PartyId == client.GetHandle<MobileParty>(player.PartyId));
        Assert.Equal(client.GetHandle<MobileParty>(player.PartyId), leaveRequest.PartyId);
        var leaveResult = Assert.Single(
            client.InternalMessages.GetMessages<NetworkSettlementEncounterLeaveResult>(),
            message => message.PartyId == client.GetHandle<MobileParty>(player.PartyId));
        Assert.Equal(SettlementEncounterLeaveOutcome.Suppressed, leaveResult.Outcome);
        Assert.DoesNotContain(
            Server.NetworkSentMessages.GetMessages<NetworkPartyLeaveSettlement>(),
            message => message.PartyId == Server.GetHandle<MobileParty>(player.PartyId));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Same(settlement, party.CurrentSettlement);
        });

        Server.PumpGameThread();
    }

    [Fact]
    public void ServerKingdomCreationRequest_SuppressesAccidentalSettlementLeaveRequest()
    {
        var player = CreateSyncedPlayerContext();
        var settlementId = CreateSyncedSettlement();
        var client = Clients.First();
        TestEnvironment.ConnectRegisteredPlayer(client, ControllerId);

        Server.SimulateMessage(
            client.NetPeer,
            new NetworkRequestCreateKingdom(ControllerId, KingdomName, player.CultureId, player.PartyId, settlementId));
        Server.SimulateMessage(client.NetPeer, new NetworkRequestEndSettlementEncounter(
            Server.GetHandle<MobileParty>(player.PartyId)));
        Server.SimulateMessage(this, new PartyLeaveSettlementAttempted(GetObject<MobileParty>(Server, player.PartyId)));

        var leaveResult = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkSettlementEncounterLeaveResult>(),
            message => message.PartyId == Server.GetHandle<MobileParty>(player.PartyId));
        Assert.Equal(SettlementEncounterLeaveOutcome.Suppressed, leaveResult.Outcome);
        Assert.DoesNotContain(
            Server.NetworkSentMessages.GetMessages<NetworkPartyLeaveSettlement>(),
            message => message.PartyId == Server.GetHandle<MobileParty>(player.PartyId));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Same(settlement, party.CurrentSettlement);
        });
        AssertCompletedSettlementProtectionDisarms(Server, player.PartyId, settlementId);

        Server.PumpGameThread();
    }

    [Fact]
    public void PlayerKingdomCreatedNotification_SuppressesStaleEndEncounterAfterCompletion()
    {
        var player = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var client = Clients.First();
        var settlementId = CreateSyncedSettlement();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }

            Assert.Same(settlement, party.CurrentSettlement);
        });

        var handler = client.Resolve<ClientKingdomHandler>();
        var pending = AccessTools.Method(typeof(ClientKingdomHandler), "CapturePendingSettlementRestore")
            .Invoke(handler, Array.Empty<object>());
        Assert.NotNull(pending);
        AccessTools.Field(typeof(ClientKingdomHandler), "pendingKingdomCreationSettlement")
            .SetValue(handler, pending);

        client.SimulateMessage(
            this,
            new NetworkPlayerKingdomCreated(ControllerId, kingdomId, KingdomName, player.ClanId, player.PartyId, settlementId));
        client.SimulateMessage(
            this,
            new NetworkSettlementEncounterLeaveResult(
                client.GetHandle<MobileParty>(player.PartyId),
                SettlementEncounterLeaveOutcome.Suppressed));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Same(settlement, party.CurrentSettlement);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SwitchToPlayer_FollowsRegisteredPartyWithoutChangingItsActivation(bool active)
    {
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        var player = CreateSyncedPlayerContext(ControllerId, _ => false);
        var throwawayClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Clan>(throwawayClanId, out var throwawayClan));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
            using (new AllowedThread())
            {
                Hero.MainHero.Clan = throwawayClan;
                Campaign.Current.PlayerDefaultFaction = throwawayClan;
                party.IsActive = active;
            }

            Campaign.Current.CameraFollowParty = MobileParty.MainParty.Party;
            Assert.NotSame(party, MobileParty.MainParty);
            Assert.NotSame(party.Party, Campaign.Current.CameraFollowParty);
            Assert.False(hero.IsPrisoner);

            client.Resolve<IHeroInterface>().SwitchToPlayer(new Player(
                ControllerId, player.HeroId, player.PartyId, player.ClanId, player.CharacterId));

            Assert.Same(party, MobileParty.MainParty);
            Assert.Same(party.Party, Campaign.Current.CameraFollowParty);
            Assert.Equal(active, party.IsActive);
        }, new[] { AccessTools.Method(typeof(InteractionsInitializationHandler), "Handle", new[] { typeof(MessagePayload<PlayerHeroChanged>) }) });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void SwitchedPlayer_RefreshesArmyTrackerAndCamera_WithAssignedPartyOrCaptor(bool active, bool captive)
    {
        var client = TestEnvironment.Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        var player = CreateSyncedPlayerContext(ControllerId, _ => false);
        var captor = captive ? CreateSyncedPlayerContext(SecondControllerId, _ => false) : null;
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var armyId = TestEnvironment.CreateRegisteredObject<Army>();
        ConfigureClanInKingdom(client, player.ClanId, kingdomId);
        EnsureKingdomRegistered(client, kingdomId);

        ConfigureArmyInKingdom(client, kingdomId, armyId);

        // Throwaway clan, avoids EncyclopediaManager exception.
        var throwawayClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Clan>(throwawayClanId, out var throwawayClan));
            using (new AllowedThread())
            {
                Hero.MainHero.Clan = throwawayClan;
                Campaign.Current.PlayerDefaultFaction = throwawayClan;
            }
        });

        MapTrackerProvider provider = null;
        client.Call(() =>
        {
            // MainHero still wrong at construction; reproduces the real bug.
            Assert.True(client.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
            Assert.NotSame(playerHero, Hero.MainHero);

            provider = new MapTrackerProvider();

            Assert.True(client.ObjectManager.TryGetObject<Army>(armyId, out var army));
            Assert.DoesNotContain(
                provider.GetTrackers(),
                tracker => ReferenceEquals(tracker.TrackedObject, army));
        });

        // Act: real switch, publishes SwitchedPlayer at the end.
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var assignedParty));
            PartyBase expectedFollow = assignedParty.Party;
            using (new AllowedThread())
            {
                assignedParty.IsActive = active;
                if (captive)
                {
                    Assert.True(client.ObjectManager.TryGetObject<Hero>(player.HeroId, out var assignedHero));
                    Assert.True(client.ObjectManager.TryGetObject<MobileParty>(captor.PartyId, out var captorParty));
                    assignedHero._heroState = Hero.CharacterStates.Prisoner;
                    assignedHero.PartyBelongedToAsPrisoner = captorParty.Party;
                    // Native character switching removes and re-adds this existing prisoner.
                    captorParty.PrisonRoster.AddToCounts(assignedHero.CharacterObject, 1);
                    Assert.Equal(1, captorParty.PrisonRoster.GetTroopCount(assignedHero.CharacterObject));
                    var states = Game.Current.GameStateManager;
                    states._gameStates.Add(states.CreateState<MapState>());
                    expectedFollow = captorParty.Party;
                }
            }
            var oldParty = Campaign.Current.MainParty.Party;
            Campaign.Current.CameraFollowParty = oldParty;
            Assert.NotSame(assignedParty.Party, oldParty);
            var heroInterface = client.Resolve<IHeroInterface>();
            heroInterface.SwitchToPlayer(new Player(
                ControllerId,
                player.HeroId,
                player.PartyId,
                player.ClanId,
                player.CharacterId));
            Assert.Same(expectedFollow, Campaign.Current.CameraFollowParty);
            Assert.Equal(active, assignedParty.IsActive);
            if (captive)
                Assert.Equal(1, expectedFollow.PrisonRoster.GetTroopCount(Hero.MainHero.CharacterObject));
        }, new[]
        {
            AccessTools.Method(typeof(InteractionsInitializationHandler), "Handle", new[] { typeof(MessagePayload<PlayerHeroChanged>) }),
            // Native captivity still selects its captor; exclude the separate menu presentation handler.
            AccessTools.Method(typeof(GameInterface.Services.PlayerCaptivityService.Handlers.PlayerCaptivityClientHandler),
                "Handle_PlayerCaptivityChanged"),
            // The camera and prisoner roster are exercised without opening the captivity UI.
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu), new[] { typeof(string) }),
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu), new[] { typeof(string) }),
        });
        GameThread.Run(() => { }, blocking: true);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Army>(armyId, out var army));
            Assert.Contains(
                provider.GetTrackers(),
                tracker => ReferenceEquals(tracker.TrackedObject, army));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SwitchToPlayer_UnresolvedRegistrationPreservesCamera(bool resolveHero)
    {
        var client = TestEnvironment.Clients.First();
        var player = CreateSyncedPlayerContext(ControllerId, _ => false);
        client.Call(() =>
        {
            var previousTarget = Campaign.Current.MainParty.Party;
            Campaign.Current.CameraFollowParty = previousTarget;
            client.Resolve<IHeroInterface>().SwitchToPlayer(new Player(
                ControllerId, resolveHero ? player.HeroId : "missing-hero",
                "missing-party", player.ClanId, player.CharacterId));
            Assert.Same(previousTarget, Campaign.Current.CameraFollowParty);
            Assert.Same(previousTarget, Campaign.Current.MainParty.Party);
        });
    }

    [Fact]
    public void ArmyCreatedDuringSession_IsPickedUpByExistingClientMapTracker()
    {
        var client = TestEnvironment.Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        var player = CreateSyncedPlayerContext(ControllerId, _ => false);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        ConfigureClanInKingdom(player.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);

        var settlementId = CreateSyncedSettlement();

        // Gather() needs a home settlement.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            using (new AllowedThread())
            {
                hero._homeSettlement = settlement;
            }
        });

        var throwawayClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Clan>(throwawayClanId, out var throwawayClan));
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            using (new AllowedThread())
            {
                throwawayClan._kingdom = kingdom;
                Hero.MainHero.Clan = throwawayClan;
                Campaign.Current.PlayerDefaultFaction = throwawayClan;
            }
        });

        // Provider exists before the army,
        // testing the live ArmyCreated listener, not ResetTrackers.
        MapTrackerProvider provider = null;
        client.Call(() => provider = new MapTrackerProvider());
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.DoesNotContain(
                provider.GetTrackers(),
                tracker => kingdom.Armies.Contains(tracker.TrackedObject as Army));
        });

        // Act: CreateArmy on the server, which syncs the events to the clients through a postfix.
        string armyId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var armyLeader));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            kingdom.CreateArmy(armyLeader, settlement, Army.ArmyTypes.Defender);

            var army = Assert.Single(kingdom.Armies);
            Assert.True(Server.ObjectManager.TryGetId(army, out armyId));
        });
        GameThread.Run(() => { }, blocking: true);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Army>(armyId, out var army));
            Assert.Contains(
                provider.GetTrackers(),
                tracker => ReferenceEquals(tracker.TrackedObject, army));
        });
    }
    private static void ConfigureArmyInKingdom(EnvironmentInstance instance, string kingdomId, string armyId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Army>(armyId, out var army));
            Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));

            using (new AllowedThread())
            {
                army.Kingdom = kingdom;
            }
        });
    }
    private static T GetObject<T>(EnvironmentInstance instance, string id) where T : class
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }

    private static void AssertCompletedSettlementProtectionDisarms(
        EnvironmentInstance instance,
        string partyId,
        string settlementId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                party.CurrentSettlement = null;
            }

            Assert.Same(settlement, party.CurrentSettlement);

            using (new AllowedThread())
            {
                party.CurrentSettlement = null;
            }

            Assert.Null(party.CurrentSettlement);
        });
    }

    private string CreateSyncedSettlement()
    {
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        ConfigureSettlementParty(Server, settlementId);
        foreach (var client in Clients)
        {
            ConfigureSettlementParty(client, settlementId);
        }

        return settlementId;
    }

    private static void ConfigureSettlementParty(EnvironmentInstance instance, string settlementId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            if (settlement.Party != null) return;

            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            using (new AllowedThread())
            {
                settlement.Party = new PartyBase(party, settlement);
            }
        });
    }

    private void SetKingdomStringIdEverywhere(string kingdomId, string stringId)
    {
        SetKingdomStringId(Server, kingdomId, stringId);
        foreach (var client in Clients)
        {
            SetKingdomStringId(client, kingdomId, stringId);
        }
    }

    private static void SetKingdomStringId(EnvironmentInstance instance, string kingdomId, string stringId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            kingdom.StringId = stringId;
        });
    }

    private static void RemoveReverseObjectManagerId(EnvironmentInstance instance, object obj)
    {
        var table = (ConditionalWeakTable<object, string>)AccessTools
            .Field(typeof(ObjectManager), "objsIds")
            .GetValue(instance.ObjectManager);

        Assert.True(table.Remove(obj));
    }

    private static void RestoreReverseObjectManagerId(
        EnvironmentInstance instance,
        object obj,
        string id)
    {
        var table = (ConditionalWeakTable<object, string>)AccessTools
            .Field(typeof(ObjectManager), "objsIds")
            .GetValue(instance.ObjectManager);

        table.Add(obj, id);
    }

    [Fact]
    public void KingdomDecisionVoting_LogsRoundOpeningVoteReceiptAndCounting()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(Server, player1.ClanId, kingdomId);
        ConfigureClanInKingdom(Server, player2.ClanId, kingdomId);

        var lines = CaptureKingdomVoteLog(() =>
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
                Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

                using (new AllowedThread())
                {
                    kingdom._unresolvedDecisions ??= new MBList<KingdomDecision>();
                    kingdom._unresolvedDecisions.Add(
                        new KingdomPolicyDecision(proposerClan, PolicyObject.All.First(), false));
                }

                KingdomDecision decision = Assert.Single(kingdom.UnresolvedDecisions);
                GetVoteManager(Server).RegisterDecision(decision);

                var vote = new KingdomDecisionVoteData(
                    kingdomId,
                    decisionIndex: 0,
                    outcomeIndex: 0,
                    supportWeight: (int)Supporter.SupportWeights.FullyPush,
                    isAbstain: false,
                    isFinal: true);
                Server.Resolve<IMessageBroker>().Publish(
                    this, new NetworkRequestKingdomDecisionVote(ControllerId, vote));
            });
        });

        Assert.Contains(lines, line =>
            line.Contains("Kingdom decision voting round opened") &&
            line.Contains(nameof(KingdomPolicyDecision)) &&
            line.Contains("2 eligible clans"));
        Assert.Contains(lines, line => line.Contains("Received kingdom decision vote from controller"));
        Assert.Contains(lines, line =>
            line.Contains("Kingdom decision vote for") && line.Contains("counted"));
    }

    [Fact]
    public void KingdomDecisionFinalSelection_LogsWhyItStaysLocalForAClanOutsideTheDecidingKingdom()
    {
        var client = Clients.First();
        var player = CreateSyncedPlayerContext(ControllerId, client);
        var otherKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        // Setting up the election asks every clan of the deciding kingdom whether its leader is the
        // local player, so the proposing clan needs a leader hero.
        var proposerClanId = CreateSyncedNpcClan();

        ConfigureClanInKingdom(client, proposerClanId, otherKingdomId);

        var lines = CaptureKingdomVoteLog(() =>
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Kingdom>(otherKingdomId, out var kingdom));
                Assert.True(client.ObjectManager.TryGetObject<Clan>(proposerClanId, out var proposerClan));

                using (new AllowedThread())
                {
                    kingdom._unresolvedDecisions ??= new MBList<KingdomDecision>();
                    kingdom._unresolvedDecisions.Add(
                        new KingdomPolicyDecision(proposerClan, PolicyObject.All.First(), false));
                }

                KingdomDecision decision = Assert.Single(kingdom.UnresolvedDecisions);
                var election = new KingdomElection(decision);
                var decisionItem = ObjectHelper.SkipConstructor<DecisionItemBaseVM>();
                decisionItem.DecisionOptionsList = new MBBindingList<DecisionOptionVM>();
                decisionItem.KingdomDecisionMaker = election;

                Assert.False(GetVoteManager(client).ShouldBlockLocalResolution(decisionItem));
            });
        });

        Assert.Contains(lines, line =>
            line.Contains("Kingdom decision final selection") && line.Contains("resolved locally"));
    }

    // Collects what the mod writes while the action runs. The voting path reports through the shared
    // logger, so the callback of the output sink is the seam that sees it.
    private static IReadOnlyList<string> CaptureKingdomVoteLog(Action action)
    {
        var lines = new List<string>();
        void Collect(string line)
        {
            lock (lines)
            {
                lines.Add(line);
            }
        }

        OutputSinkManager.AddLogCallback(Collect);
        try
        {
            action();
        }
        finally
        {
            OutputSinkManager.RemoveLogCallback(Collect);
        }

        lock (lines)
        {
            return lines.ToList();
        }
    }

    // Two player clans vote on declare-war decisions proposed by the first player's clan.
    private (EnvironmentInstance Client1, EnvironmentInstance Client2, PlayerContext Player1, PlayerContext Player2, string KingdomId)
        SetUpPlayerDeclareWarVotes(string targetControllerId, int decisionCount = 1)
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);

        var targetKingdomIds = new List<string>();
        for (int i = 0; i < decisionCount; i++)
        {
            var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
            var targetPlayer = CreateSyncedPlayerContext(targetControllerId + i, _ => false);
            ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
            EnsureKingdomRegisteredEverywhere(targetKingdomId);
            targetKingdomIds.Add(targetKingdomId);
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));
            foreach (string targetKingdomId in targetKingdomIds)
            {
                Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
                kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
            }
        });

        return (client1, client2, player1, player2, kingdomId);
    }

    private static DecisionItemBaseVM SubmitDeclareWarVoteFromPanel(KingdomDecisionsVM decisionsVm, KingdomDecision decision)
    {
        decisionsVm.RefreshWith(decision);
        return SubmitCurrentDecisionVote(decisionsVm, outcome => IsDeclareWarOutcome(outcome, true));
    }

    private static DecisionItemBaseVM SubmitCurrentDecisionVote(KingdomDecisionsVM decisionsVm, Func<DecisionOutcome, bool> isChosenOutcome)
    {
        DecisionItemBaseVM decisionItem = decisionsVm.CurrentDecision;
        DecisionOptionVM option = decisionItem.DecisionOptionsList.Single(candidate => isChosenOutcome(candidate.Option));
        option.CurrentSupportWeight = Supporter.SupportWeights.FullyPush;
        decisionItem._currentSelectedOption = option;
        decisionItem.ExecuteFinalSelection();
        return decisionItem;
    }

    private static bool IsFiefOwnerChangeOutcome(DecisionOutcome outcome)
    {
        return outcome is SettlementClaimantPreliminaryDecision.SettlementClaimantPreliminaryOutcome preliminaryOutcome &&
               preliminaryOutcome.ShouldSettlementOwnerChange;
    }

    // Player heroes count as human players only when they lead their party, which the shared context leaves unset.
    private void LeadPlayerPartiesEverywhere(params PlayerContext[] players)
    {
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                foreach (PlayerContext player in players)
                {
                    Assert.True(instance.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
                    Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
                    using (new AllowedThread())
                    {
                        party._partyComponent = new LordPartyComponent(hero, hero, null);
                    }

                    Assert.True(hero.IsHumanPlayerCharacter);
                }
            });
        }
    }

    // A town owned by the clan with what the fief decisions and their panel read.
    private string CreateSyncedKingdomTown(string ownerClanId, string cultureId)
    {
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(ownerClanId, out var ownerClan));
                Assert.True(instance.ObjectManager.TryGetObject<CultureObject>(cultureId, out var culture));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(townId, out var town));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

                using (new AllowedThread())
                {
                    culture.Name ??= new TextObject("Empire");
                    settlement._name = new TextObject("Danustica");
                    settlement.Culture = culture;
                    settlement._boundVillages ??= new MBList<Village>();
                    settlement._notablesCache ??= new MBList<Hero>();
                    settlement.SetSettlementComponent(town);
                    town.Buildings ??= new MBList<Building>();
                    town._ownerClan = ownerClan;
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                }

                Assert.Same(ownerClan, settlement.OwnerClan);
                Assert.Same(ownerClan.Kingdom, settlement.MapFaction);
            });
        }

        return settlementId;
    }

    private static IReadOnlyList<InquiryData> CaptureInquiries(Action action)
    {
        var inquiries = new List<InquiryData>();
        Action<InquiryData, bool, bool> onShowInquiry = (data, _, _) => inquiries.Add(data);
        EventInfo showInquiryEvent = typeof(InformationManager).GetEvent(
            "OnShowInquiry",
            BindingFlags.Public | BindingFlags.Static);
        showInquiryEvent.AddEventHandler(null, onShowInquiry);
        try
        {
            action();
        }
        finally
        {
            showInquiryEvent.RemoveEventHandler(null, onShowInquiry);
        }

        return inquiries;
    }

    private static KingdomDecisionRoundStatusData CreateTwoClanRoundStatus(
        string kingdomId,
        string localClanId,
        string remoteClanId,
        int secondsLeft)
    {
        return new KingdomDecisionRoundStatusData(
            kingdomId,
            0,
            (DateTime.UtcNow + TimeSpan.FromSeconds(secondsLeft)).Ticks,
            new[]
            {
                new KingdomDecisionRoundClanStatusData(localClanId, localClanId, ControllerId, true, true),
                new KingdomDecisionRoundClanStatusData(remoteClanId, remoteClanId, SecondControllerId, false, true),
            });
    }

    private PlayerContext CreateSyncedPlayerContext()
    {
        return CreateSyncedPlayerContext(ControllerId, Clients.First());
    }

    private string CreateSyncedNpcClan()
    {
        const string npcControllerId = "NpcPeaceOfferEnemy";
        var npc = CreateSyncedPlayerContext(npcControllerId, _ => false);

        RemovePlayerRegistration(Server, npcControllerId);
        foreach (var client in Clients)
        {
            RemovePlayerRegistration(client, npcControllerId);
        }

        return npc.ClanId;
    }

    private static void RemovePlayerRegistration(EnvironmentInstance instance, string controllerId)
    {
        instance.Call(() =>
        {
            var playerManager = instance.Resolve<IPlayerManager>();
            Assert.True(playerManager.TryGetPlayer(controllerId, out var player));
            Assert.True(playerManager.RemovePlayer(player));
        });
    }

    private PlayerContext CreateSyncedPlayerContext(string controllerId, EnvironmentInstance localPlayerClient)
    {
        PlayerContext player = CreateSyncedPlayerContext(
            controllerId,
            instance => ReferenceEquals(instance, localPlayerClient));
        TestEnvironment.ConnectRegisteredPlayer(localPlayerClient, controllerId);
        return player;
    }

    private PlayerContext CreateSyncedPlayerContextInClan(
        string controllerId,
        EnvironmentInstance localPlayerClient,
        string clanId)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var characterId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var cultureId = TestEnvironment.CreateRegisteredObject<CultureObject>();

        ConfigurePlayerContext(
            Server,
            controllerId,
            clanId,
            heroId,
            partyId,
            characterId,
            cultureId,
            false,
            setAsClanLeader: false);
        foreach (var client in Clients)
        {
            ConfigurePlayerContext(
                client,
                controllerId,
                clanId,
                heroId,
                partyId,
                characterId,
                cultureId,
                ReferenceEquals(client, localPlayerClient),
                setAsClanLeader: false);
        }

        TestEnvironment.ConnectRegisteredPlayer(localPlayerClient, controllerId);
        return new PlayerContext(clanId, heroId, partyId, characterId, cultureId);
    }

    private PlayerContext CreateSyncedPlayerContext(
        string controllerId,
        Func<EnvironmentInstance, bool> shouldSetMainHero)
    {
        var clanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var characterId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var cultureId = TestEnvironment.CreateRegisteredObject<CultureObject>();

        ConfigurePlayerContext(Server, controllerId, clanId, heroId, partyId, characterId, cultureId, shouldSetMainHero(Server));
        foreach (var client in Clients)
        {
            ConfigurePlayerContext(client, controllerId, clanId, heroId, partyId, characterId, cultureId, shouldSetMainHero(client));
        }

        return new PlayerContext(clanId, heroId, partyId, characterId, cultureId);
    }

    private string CreateSyncedClanFief(string clanId)
    {
        var fiefId = TestEnvironment.CreateRegisteredObject<Town>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        ConfigureClanFief(Server, clanId, fiefId, settlementId);
        foreach (var client in Clients)
        {
            ConfigureClanFief(client, clanId, fiefId, settlementId);
        }

        return fiefId;
    }

    private static KingdomDecisionVoteData CreateDeclareWarVote(string kingdomId, bool isFinal)
    {
        return new KingdomDecisionVoteData(
            kingdomId,
            decisionIndex: 0,
            outcomeIndex: 0,
            supportWeight: (int)Supporter.SupportWeights.FullyPush,
            isAbstain: false,
            isFinal: isFinal);
    }

    private static KingdomDecisionVoteData CreateDeclareWarNoVote(string kingdomId, bool isFinal)
    {
        return new KingdomDecisionVoteData(
            kingdomId,
            decisionIndex: 0,
            outcomeIndex: 1,
            supportWeight: (int)Supporter.SupportWeights.FullyPush,
            isAbstain: false,
            isFinal: isFinal);
    }

    private static KingdomDecisionVoteData CreateMakePeaceVote(string kingdomId, bool shouldMakePeace)
    {
        return new KingdomDecisionVoteData(
            kingdomId,
            decisionIndex: 0,
            outcomeIndex: shouldMakePeace ? 0 : 1,
            supportWeight: (int)Supporter.SupportWeights.FullyPush,
            isAbstain: false,
            isFinal: true);
    }

    private static KingdomDecisionVoteData CreateDeclareWarVoteFromUi(EnvironmentInstance instance, bool shouldWarBeDeclared)
    {
        KingdomDecisionVoteData voteData = null;
        instance.Call(() =>
        {
            var kingdom = Clan.PlayerClan.Kingdom;
            Assert.NotNull(kingdom);
            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var decisionsVm = new KingdomDecisionsVM(() => { });
            decisionsVm.RefreshWith(decision);
            var decisionItem = decisionsVm.CurrentDecision;
            var selectedOption = decisionItem.DecisionOptionsList.Single(option =>
                IsDeclareWarOutcome(option.Option, shouldWarBeDeclared));

            selectedOption.CurrentSupportWeight = Supporter.SupportWeights.Choose;
            selectedOption.IsSelected = true;
            AccessTools.Field(typeof(DecisionItemBaseVM), "_currentSelectedOption")
                .SetValue(decisionItem, selectedOption);

            Assert.True(GetVoteManager(instance).TryCreateVoteData(decisionItem, out voteData, true));
        });
        return voteData;
    }

    private static bool IsDeclareWarOutcome(DecisionOutcome outcome, bool shouldWarBeDeclared)
    {
        FieldInfo fieldInfo = outcome?.GetType().GetField(
            "ShouldWarBeDeclared",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return fieldInfo?.FieldType == typeof(bool) &&
               (bool)fieldInfo.GetValue(outcome) == shouldWarBeDeclared;
    }

    // Builds the Resolve action through vanilla OnSetPeaceItem, wired to the decision panel like the kingdom screen.
    private static KingdomDiplomacyProposalActionItemVM SelectDiplomacyResolveAction(
        Kingdom kingdom,
        IFaction faction,
        KingdomDecisionsVM decisionsVm)
    {
        var diplomacyVm = ObjectHelper.SkipConstructor<KingdomDiplomacyVM>();
        diplomacyVm._actions = new MBBindingList<KingdomDiplomacyProposalActionItemVM>();
        AccessTools.Field(typeof(KingdomDiplomacyVM), "_playerKingdom").SetValue(diplomacyVm, kingdom);
        AccessTools.Field(typeof(KingdomDiplomacyVM), "_forceDecision")
            .SetValue(diplomacyVm, new Action<KingdomDecision>(decisionsVm.RefreshWith));

        var truceItem = ObjectHelper.SkipConstructor<KingdomTruceItemVM>();
        AccessTools.Field(typeof(KingdomDiplomacyItemVM), "Faction1").SetValue(truceItem, kingdom);
        AccessTools.Field(typeof(KingdomDiplomacyItemVM), "Faction2").SetValue(truceItem, faction);
        diplomacyVm.OnSetPeaceItem(truceItem);

        string resolveText = GameTexts.FindText("str_resolve").ToString();
        return Assert.Single(diplomacyVm.Actions, action => action.Name == resolveText);
    }

    // Selects the policy through vanilla OnPolicySelect, wired to the decision panel like the kingdom screen.
    private static KingdomPoliciesVM SelectPolicyWithResolve(KingdomPolicyDecision decision, KingdomDecisionsVM decisionsVm)
    {
        var policiesVm = ObjectHelper.SkipConstructor<KingdomPoliciesVM>();
        policiesVm._doneHint = new HintViewModel();
        AccessTools.Field(typeof(KingdomPoliciesVM), "_forceDecide")
            .SetValue(policiesVm, new Action<KingdomDecision>(decisionsVm.RefreshWith));

        var policyItem = ObjectHelper.SkipConstructor<KingdomPolicyItemVM>();
        policyItem._policy = decision.Policy;
        policiesVm.OnPolicySelect(policyItem);

        Assert.Same(decision, policiesVm._currentItemsUnresolvedDecision);
        return policiesVm;
    }

    private static void AssertClosableWaitingViewCloses(KingdomDecisionsVM decisionsVm, KingdomDecision decision)
    {
        DecisionItemBaseVM decisionItem = decisionsVm.CurrentDecision;
        Assert.NotNull(decisionItem);
        Assert.Same(decision, decisionItem.KingdomDecisionMaker._decision);
        Assert.True(decisionItem.IsActive);
        Assert.True(decisionItem._finalSelectionDone);
        Assert.True(decisionItem.CanEndDecision);
        Assert.All(decisionItem.DecisionOptionsList, candidate => Assert.False(candidate.CanBeChosen));

        decisionItem.ExecuteFinalSelection();

        Assert.Null(decisionsVm.CurrentDecision);
        Assert.False(decisionItem.IsActive);
    }

    private static void AssertOpenBallot(KingdomDecisionsVM decisionsVm, KingdomDecision decision)
    {
        DecisionItemBaseVM decisionItem = decisionsVm.CurrentDecision;
        Assert.NotNull(decisionItem);
        Assert.Same(decision, decisionItem.KingdomDecisionMaker._decision);
        Assert.True(decisionItem.IsActive);
        Assert.False(decisionItem._finalSelectionDone);
        Assert.Contains(decisionItem.DecisionOptionsList, candidate => candidate.CanBeChosen);
    }

    private static void ConfigureClanFief(EnvironmentInstance instance, string clanId, string fiefId, string settlementId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
            Assert.True(instance.ObjectManager.TryGetObject<Town>(fiefId, out var fief));
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                if (settlement.Town == null)
                {
                    fief.Owner = settlement.Party;
                    settlement.Town = fief;
                }
                fief._ownerClan = clan;
                clan._fiefsCache ??= new MBList<Town>();
                if (!clan._fiefsCache.Contains(fief))
                {
                    clan._fiefsCache.Add(fief);
                }
            }

            Assert.Contains(fief, clan.Fiefs);
        });
    }

    private void ConfigureClanInKingdom(string clanId, string kingdomId)
    {
        ConfigureClanInKingdom(Server, clanId, kingdomId);
        foreach (var client in Clients)
        {
            ConfigureClanInKingdom(client, clanId, kingdomId);
        }
    }

    private void ConfigureWarEverywhere(string faction1Id, string faction2Id)
    {
        ConfigureWar(Server, faction1Id, faction2Id);
        foreach (var client in Clients)
        {
            ConfigureWar(client, faction1Id, faction2Id);
        }
    }

    private static void ConfigureWar(EnvironmentInstance instance, string faction1Id, string faction2Id)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(faction1Id, out var faction1));
            Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(faction2Id, out var faction2));
            VillageHostileFactionStanceHelper.ApplyWarStance(faction1, faction2);
            Assert.True(FactionManager.IsAtWarAgainstFaction(faction1, faction2));
        });
    }

    /// <summary>
    /// The debug create command inherits the new kingdom's culture from the ruling clan, which the shared
    /// player context leaves unset.
    /// </summary>
    private void SetClanCultureEverywhere(string clanId, string cultureId)
    {
        SetClanCulture(Server, clanId, cultureId);
        foreach (var client in Clients)
        {
            SetClanCulture(client, clanId, cultureId);
        }
    }

    private static void SetClanCulture(EnvironmentInstance instance, string clanId, string cultureId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
            Assert.True(instance.ObjectManager.TryGetObject<CultureObject>(cultureId, out var culture));

            using (new AllowedThread())
            {
                clan.Culture = culture;
            }
        });
    }

    private void SetClanTierEverywhere(string clanId, int tier)
    {
        SetClanTier(Server, clanId, tier);
        foreach (var client in Clients)
        {
            SetClanTier(client, clanId, tier);
        }
    }

    private static void AssertVassalMembership(
        EnvironmentInstance instance,
        string clanId,
        string kingdomId)
    {
        Assert.True(instance.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
        Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
        Assert.Same(kingdom, clan.Kingdom);
        Assert.Contains(clan, kingdom.Clans);
        Assert.False(clan.IsUnderMercenaryService);
    }

    private static void SetClanTier(EnvironmentInstance instance, string clanId, int tier)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
            using (new AllowedThread())
            {
                clan._tier = tier;
            }
        });
    }

    private static void ConfigureClanInKingdom(EnvironmentInstance instance, string clanId, string kingdomId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
            Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));

            using (new AllowedThread())
            {
                clan._kingdom = kingdom;
                kingdom._rulingClan ??= clan;
                kingdom._clans ??= new MBList<Clan>();
                if (!kingdom._clans.Contains(clan))
                {
                    kingdom._clans.Add(clan);
                }
            }
        });
    }

    private static void ConfigurePlayerContext(
        EnvironmentInstance instance,
        string controllerId,
        string clanId,
        string heroId,
        string partyId,
        string characterId,
        string cultureId,
        bool setAsMainHero,
        bool setAsClanLeader = true)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));
            Assert.True(instance.ObjectManager.TryGetObject<CultureObject>(cultureId, out var culture));

            using (new AllowedThread())
            {
                clan.Name = new TextObject("realclan");
                hero.Clan = clan;
                if (setAsClanLeader) clan.SetLeader(hero);
                character.HeroObject = hero;
                if (setAsMainHero)
                {
                    Game.Current.PlayerTroop = character;
                    Campaign.Current.PlayerDefaultFaction = clan;
                }
                hero.PartyBelongedTo = party;
                party.ActualClan = clan;
                character.Culture = culture;
                if (!Campaign.Current.CampaignObjectManager._mobileParties.Contains(party))
                {
                    Campaign.Current.CampaignObjectManager.AddMobileParty(party);
                }
            }

            var registry = instance.Resolve<IPlayerManager>();
            Assert.True(registry.AddPlayer(new Player(controllerId, heroId, partyId, clanId, characterId)));
            Assert.True(registry.TryGetPlayer(controllerId, out var _));
        });
    }

    private void EnsureKingdomRegisteredEverywhere(string kingdomId)
    {
        EnsureKingdomRegistered(Server, kingdomId);
        foreach (var client in Clients)
        {
            EnsureKingdomRegistered(client, kingdomId);
        }
    }

    private static void EnsureKingdomRegistered(EnvironmentInstance instance, string kingdomId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));

            using (new AllowedThread())
            {
                kingdom._activePolicies ??= new MBList<PolicyObject>();
                kingdom._armies ??= new MBList<Army>();
                kingdom._clans ??= new MBList<Clan>();
                kingdom._unresolvedDecisions ??= new MBList<KingdomDecision>();
                kingdom._factionsAtWarWith ??= new MBList<IFaction>();
                kingdom._alliedKingdoms ??= new MBList<Kingdom>();

                if (!Campaign.Current.CampaignObjectManager.Kingdoms.Contains(kingdom))
                {
                    Campaign.Current.CampaignObjectManager.AddKingdom(kingdom);
                }
            }
        });
    }

    private void AssertKingdomCreatedOnServer(string kingdomId, string clanId, string cultureId, string? fiefId = null)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
            Assert.True(Server.ObjectManager.TryGetObject<CultureObject>(cultureId, out var culture));

            Assert.Equal(KingdomName, kingdom.Name.ToString());
            Assert.Same(clan, kingdom.RulingClan);
            Assert.Same(culture, kingdom.Culture);
            Assert.Same(kingdom, clan.Kingdom);
            Assert.Contains(clan, kingdom.Clans);
            AssertKingdomContainsFief(Server.ObjectManager, kingdom, fiefId);
            Assert.Contains(kingdom, Campaign.Current.CampaignObjectManager.Kingdoms);
        });
    }

    private void AssertKingdomSyncedToClient(
        EnvironmentInstance client,
        string kingdomId,
        string clanId,
        string cultureId,
        string? fiefId = null)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
            Assert.True(client.ObjectManager.TryGetObject<CultureObject>(cultureId, out var culture));

            Assert.Equal(KingdomName, kingdom.Name.ToString());
            Assert.Same(clan, kingdom.RulingClan);
            Assert.Same(culture, kingdom.Culture);
            Assert.Same(kingdom, clan.Kingdom);
            AssertKingdomReadyForManagementUi(kingdom, clan);
            AssertKingdomContainsFief(client.ObjectManager, kingdom, fiefId);
            Assert.Contains(kingdom, Campaign.Current.CampaignObjectManager.Kingdoms);
        });
    }

    private static void AssertKingdomContainsFief(IObjectManager objectManager, Kingdom kingdom, string? fiefId)
    {
        if (string.IsNullOrWhiteSpace(fiefId)) return;

        Assert.True(objectManager.TryGetObject<Town>(fiefId, out var fief));
        Assert.Contains(fief, kingdom._fiefsCache);
        Assert.Contains(fief, kingdom.Fiefs);
        if (fief.IsTown)
        {
            Assert.Contains(fief, kingdom._townsCache);
            Assert.Contains(fief, kingdom.Towns);
        }
    }

    private static void AssertKingdomReadyForManagementUi(Kingdom kingdom, Clan clan)
    {
        Assert.NotNull(kingdom._activePolicies);
        Assert.NotNull(kingdom._armies);
        Assert.NotNull(kingdom._clans);
        Assert.NotNull(kingdom._unresolvedDecisions);
        Assert.NotNull(kingdom._factionsAtWarWith);
        Assert.NotNull(kingdom._alliedKingdoms);
        Assert.NotNull(kingdom._fiefsCache);
        Assert.NotNull(kingdom._townsCache);
        Assert.NotNull(kingdom._settlementsCache);
        Assert.NotNull(kingdom._villagesCache);
        Assert.NotNull(kingdom._heroesCache);
        Assert.NotNull(kingdom._aliveLordsCache);
        Assert.NotNull(kingdom._deadLordsCache);
        Assert.NotNull(kingdom._warPartyComponentsCache);
        Assert.Contains(clan, kingdom.Clans);
        Assert.Empty(kingdom.UnresolvedDecisions);
        _ = kingdom.ActivePolicies.Count;
    }

    private record PlayerContext(
        string ClanId,
        string HeroId,
        string PartyId,
        string CharacterId,
        string CultureId);

    private static string ExecuteCommand(ICoopCommand command, params string[] args)
    {
        return command.ProcessCommand(new CoopCommandArgsFactory().FromValues(args)).Output;
    }
}
