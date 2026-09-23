using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Core.Server.Services.Kingdoms.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.Buildings.Messages;
using GameInterface.Services.Buildings.Patches;
using GameInterface.Services.Clans;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.CampaignService.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.HeirSelection.Interfaces;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Settlements;
using GameInterface.Services.Settlements.Messages;
using GameInterface.Services.Workshops.Interfaces;
using HarmonyLib;
using SandBox.GauntletUI.Menu;
using SandBox.View.Menu;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.TownManagement;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Clans;

public class CoopClanFlowTests : MapEventTestBase, IDisposable
{
    private readonly Harmony presentationPatch = new($"coop-clan-presentation-{Guid.NewGuid()}");
    private readonly EnvironmentInstance leaderClient;
    private readonly EnvironmentInstance memberClient;
    private readonly Player leader;
    private readonly Player member;

    public CoopClanFlowTests(ITestOutputHelper output) : base(output)
    {
        // Registration and campaign state are tested here; switching the live map requires a loaded scene.
        presentationPatch.Patch(AccessTools.Method(typeof(ChangePlayerCharacterAction), nameof(ChangePlayerCharacterAction.Apply)),
            prefix: new HarmonyMethod(typeof(CoopClanFlowTests), nameof(SkipMapCharacterSwitch)) { priority = Priority.First });
        leaderClient = Clients.First();
        memberClient = Clients.Last();
        leader = CreatePlayer(leaderClient, "clan-leader");
        member = CreatePlayer(memberClient, "clan-member");
        Flush();
    }

    public new void Dispose()
    {
        presentationPatch.UnpatchAll(presentationPatch.Id);
        base.Dispose();
    }

    public static bool SkipMapCharacterSwitch() => false;

    public static bool SkipTownViewTeardown(MenuViewContext __instance)
    {
        AccessTools.Field(typeof(MenuViewContext), "_menuTownManagement").SetValue(__instance, null);
        return false;
    }

    // T06: a stale town screen must not commit its queue, default project or reserve after removal.
    [Fact]
    public void RemovedMember_LosesTownAccessAndCannotCommitStaleBuildingChanges()
    {
        Join();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var buildingId = TestEnvironment.CreateRegisteredObject<Building>();
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                using var scope = new AllowedThread();
                var town = Get<Town>(instance, townId);
                var settlement = Get<Settlement>(instance, settlementId);
                settlement.Town = town;
                town.Owner = settlement.Party;
                town._ownerClan = Get<Clan>(instance, leader.ClanId);
                town.Buildings ??= new();
                town.BuildingsInProgress ??= new();
                town._tradeBoundVillagesCache ??= new();
                var building = Get<Building>(instance, buildingId);
                building.Town = town;
                town.Buildings.Add(building);
                Get<MobileParty>(instance, member.MobilePartyId).CurrentSettlement = settlement;
                Get<MobileParty>(instance, leader.MobilePartyId).CurrentSettlement = settlement;
            });
        }

        Send(memberClient, new RequestSettlementMenuAccess(settlementId, "manage_production", true));
        Server.Call(() => Assert.Equal(member.HeroId,
            Assert.Single(Server.Resolve<ISettlementMenuAccess>().GetOpenMenus()).HeroId));

        // Exercise the view's close decision without allocating a Gauntlet rendering layer.
        presentationPatch.Patch(AccessTools.Method(typeof(MenuViewContext), nameof(MenuViewContext.CloseTownManagement)),
            prefix: new HarmonyMethod(typeof(CoopClanFlowTests), nameof(SkipTownViewTeardown)));
        var view = new GauntletMenuTownManagementView();
        var vm = ObjectHelper.SkipConstructor<TownManagementVM>();
        var context = ObjectHelper.SkipConstructor<MenuViewContext>();
        AccessTools.Field(typeof(GauntletMenuTownManagementView), "_dataSource").SetValue(view, vm);
        AccessTools.Property(typeof(MenuView), nameof(MenuView.MenuViewContext)).SetValue(view, context);
        AccessTools.Field(typeof(MenuViewContext), "_menuTownManagement").SetValue(context, view);
        memberClient.Call(() =>
        {
            AccessTools.Field(typeof(TownManagementVM), "_settlement").SetValue(vm, Get<Settlement>(memberClient, settlementId));
            Assert.True(TownManagementViewPatches.OnFrameTickPrefix(view));
        });

        Send(memberClient, new ChangeCurrentBuildingQueue(new() { buildingId }, townId));
        Send(memberClient, new BoostBuildingProcessWithGold(100, townId, member.HeroId));
        Flush();
        Server.Call(() =>
        {
            Assert.Single(Get<Town>(Server, townId).BuildingsInProgress);
            Assert.Equal(100, Get<Town>(Server, townId).BoostBuildingProcess);
        });

        Send(leaderClient, new RequestClanMemberLeave(leader.HeroId, member.HeroId));
        Flush();
        memberClient.Call(() =>
        {
            Assert.False(TownManagementViewPatches.OnFrameTickPrefix(view));
            Assert.Null(AccessTools.Field(typeof(MenuViewContext), "_menuTownManagement").GetValue(context));
            var sentBefore = memberClient.NetworkSentMessages.GetMessages<ChangeCurrentBuildingQueue>().Count();
            var town = Get<Town>(memberClient, townId);
            Helpers.BuildingHelper.ChangeCurrentBuildingQueue(new(), town);
            Helpers.BuildingHelper.ChangeDefaultBuilding(Get<Building>(memberClient, buildingId), town);
            Helpers.BuildingHelper.BoostBuildingProcessWithGold(0, town);
            Assert.Equal(sentBefore, memberClient.NetworkSentMessages.GetMessages<ChangeCurrentBuildingQueue>().Count());
            Assert.Empty(memberClient.NetworkSentMessages.GetMessages<ChangeDefaultBuilding>());
            Assert.Equal(100, town.BoostBuildingProcess);
        });
        // These requests represent edits sent before the removed client's screen has closed.
        Send(memberClient, new ChangeCurrentBuildingQueue(new(), townId));
        Send(memberClient, new ChangeDefaultBuilding(buildingId, townId));
        Send(memberClient, new BoostBuildingProcessWithGold(0, townId, member.HeroId));
        Flush();

        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Assert.Empty(instance.Resolve<ISettlementMenuAccess>().GetOpenMenus());
                var town = Get<Town>(instance, townId);
                Assert.Single(town.BuildingsInProgress);
                Assert.False(Get<Building>(instance, buildingId).IsCurrentlyDefault);
                Assert.Equal(100, town.BoostBuildingProcess);
                Assert.Equal(9900, Get<Hero>(instance, member.HeroId).Gold);
                Assert.False(SettlementMenuAccess.CanUseSettlement(Get<Hero>(instance, member.HeroId), town.Settlement));
            });
        }

        Send(memberClient, new RequestSettlementMenuAccess(settlementId, "manage_production", true));
        Send(leaderClient, new RequestSettlementMenuAccess(settlementId, "manage_production", true));
        Flush();
        Server.Call(() => Assert.Equal(leader.HeroId,
            Assert.Single(Server.Resolve<ISettlementMenuAccess>().GetOpenMenus()).HeroId));
    }

    [Fact]
    public void JoinedMemberAppointedLeader_ReceivesKingdomDecisionsWithoutReconnect()
    {
        Join();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                using var scope = new AllowedThread();
                var joinedClan = Get<Clan>(instance, leader.ClanId);
                var appointedLeader = Get<Hero>(instance, member.HeroId);
                var kingdom = Get<Kingdom>(instance, kingdomId);

                joinedClan.SetLeader(appointedLeader);
                joinedClan._kingdom = kingdom;
                kingdom._rulingClan = joinedClan;
                kingdom._clans ??= new();
                if (!kingdom._clans.Contains(joinedClan)) kingdom._clans.Add(joinedClan);

                Assert.True(instance.Resolve<IPlayerManager>().TryGetPlayer(member.ControllerId, out var registeredPlayer));
                Assert.Equal(member.ClanId, registeredPlayer.ClanId);
                Assert.NotSame(Get<Clan>(instance, registeredPlayer.ClanId), appointedLeader.Clan);
                Assert.Same(appointedLeader, joinedClan.Leader);
            });
        }

        var decisionData = new DeclareWarDecisionData(
            leader.ClanId,
            kingdomId,
            CampaignTime.Now._numTicks,
            false,
            false,
            false,
            targetKingdomId);

        memberClient.SimulateMessage(
            Server.NetPeer,
            new NetworkAddDecision(kingdomId, decisionData, false, 0.5f));

        Assert.Contains(
            memberClient.InternalMessages.GetMessages<AddDecision>(),
            message => message.KingdomId == kingdomId);
        memberClient.Call(() =>
        {
            var kingdom = Get<Kingdom>(memberClient, kingdomId);
            Assert.Single(kingdom.UnresolvedDecisions);
            Assert.IsType<DeclareWarDecision>(kingdom.UnresolvedDecisions[0]);
        });
    }

    // A01, A07, A08, L05, L07, P02: transfer, assign roles as a member, then leave with eligible family.
    [Fact]
    public void JoinThenLeave_FamilyFollowsButSharedRelativesCompanionsAndSupportersStay()
    {
        var spouseId = TestEnvironment.CreateRegisteredObject<Hero>();
        var childId = TestEnvironment.CreateRegisteredObject<Hero>();
        var sisterId = TestEnvironment.CreateRegisteredObject<Hero>();
        var parentId = TestEnvironment.CreateRegisteredObject<Hero>();
        var nephewId = TestEnvironment.CreateRegisteredObject<Hero>();
        var companionId = TestEnvironment.CreateRegisteredObject<Hero>();
        var supporterId = TestEnvironment.CreateRegisteredObject<Hero>();
        Server.Call(() =>
        {
            var memberHero = Get<Hero>(Server, member.HeroId);
            var clan = memberHero.Clan;
            var spouse = Get<Hero>(Server, spouseId);
            var child = Get<Hero>(Server, childId);
            var sister = Get<Hero>(Server, sisterId);
            var nephew = Get<Hero>(Server, nephewId);
            foreach (var relative in new[] { spouse, child, sister, nephew })
            {
                relative.Occupation = Occupation.Lord;
                relative.Clan = clan;
            }
            memberHero.Spouse = spouse;
            spouse.Spouse = memberHero;
            child.Father = memberHero;
            child.Mother = spouse;
            memberHero.Father = sister.Father = Get<Hero>(Server, parentId);
            nephew.Mother = sister;
            nephew.Father = Get<Hero>(Server, leader.HeroId);
            var companion = Get<Hero>(Server, companionId);
            companion.CompanionOf = clan;
            var party = Get<MobileParty>(Server, member.MobilePartyId);
            party.MemberRoster.AddToCounts(child.CharacterObject, 1);
            party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
            party.SetPartyScout(companion);
            Get<Hero>(Server, supporterId).SupporterOf = clan;
        });
        Join();
        var roles = new (Action<MobileParty, Hero> Assign, Func<MobileParty, Hero> Read)[]
        {
            ((party, hero) => party.SetPartyScout(hero), party => party.Scout),
            ((party, hero) => party.SetPartySurgeon(hero), party => party.Surgeon),
            ((party, hero) => party.SetPartyEngineer(hero), party => party.Engineer),
            ((party, hero) => party.SetPartyQuartermaster(hero), party => party.Quartermaster),
        };
        foreach (var role in roles)
        {
            memberClient.Call(() => role.Assign(Get<MobileParty>(memberClient, member.MobilePartyId), Get<Hero>(memberClient, companionId)));
            Flush();
            foreach (var instance in Clients.Prepend(Server))
                instance.Call(() => Assert.Same(Get<Hero>(instance, companionId), role.Read(Get<MobileParty>(instance, member.MobilePartyId))));
            memberClient.Call(() => role.Assign(Get<MobileParty>(memberClient, member.MobilePartyId), null));
            Flush();
            foreach (var instance in Clients.Prepend(Server))
                instance.Call(() => Assert.Null(role.Read(Get<MobileParty>(instance, member.MobilePartyId))));
        }
        memberClient.Call(() => Get<MobileParty>(memberClient, member.MobilePartyId).SetPartyScout(Get<Hero>(memberClient, companionId)));
        Flush();
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                var clan = Get<Clan>(instance, leader.ClanId);
                foreach (var id in new[] { member.HeroId, spouseId, childId, sisterId, nephewId, companionId })
                    Assert.Same(clan, Get<Hero>(instance, id).Clan);
                Assert.Same(clan, Get<Hero>(instance, supporterId).SupporterOf);
            });
        }

        Send(memberClient, new RequestClanMemberLeave(member.HeroId, member.HeroId));
        Flush();
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                var originalClan = Get<Clan>(instance, member.ClanId);
                var joinedClan = Get<Clan>(instance, leader.ClanId);
                foreach (var id in new[] { member.HeroId, spouseId, childId, sisterId })
                    Assert.Same(originalClan, Get<Hero>(instance, id).Clan);
                Assert.Same(joinedClan, Get<Hero>(instance, nephewId).Clan);
                Assert.Same(joinedClan, Get<Hero>(instance, companionId).CompanionOf);
                Assert.Same(joinedClan, Get<Hero>(instance, supporterId).SupporterOf);
                var party = Get<MobileParty>(instance, member.MobilePartyId);
                Assert.Same(Get<Hero>(instance, member.HeroId), party.LeaderHero);
                Assert.Same(party, Get<Hero>(instance, childId).PartyBelongedTo);
                Assert.Null(Get<Hero>(instance, companionId).PartyBelongedTo);
                Assert.NotSame(Get<Hero>(instance, companionId), party.Scout);
            });
        }
    }

    // A03, A04, A08: transfer into empty/occupied warehouses, including a harmless repeated transfer.
    [Fact]
    public void WarehouseTransfer_MergesStockOnAllClientsWithoutReturningItOnDeparture()
    {
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var secondSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var itemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var otherItemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                var behavior = Campaign.Current.GetCampaignBehavior<WorkshopsCampaignBehavior>();
                behavior._workshopData = new WorkshopsCampaignBehavior.WorkshopData[8];
                behavior._warehouseRosterPerSettlement = new KeyValuePair<Settlement, ItemRoster>[8];
                var data = instance.Resolve<ISessionWorkshopPlayerDataInterface>();
                data.AddPlayerKeys(leader.HeroId);
                data.AddPlayerKeys(member.HeroId);
                data.AddNewWarehouseDataIfNeeded(leader.HeroId, settlementId);
                data.AddNewWarehouseDataIfNeeded(member.HeroId, settlementId);
                data.AddNewWarehouseDataIfNeeded(member.HeroId, secondSettlementId);
                var item = Get<ItemObject>(instance, itemId);
                data.UpdateWarehouseRoster(leader.HeroId, settlementId, new[] { new ItemRosterElement(item, 5) });
                data.UpdateWarehouseRoster(member.HeroId, settlementId, new[] { new ItemRosterElement(item, 10) });
                data.UpdateWarehouseRoster(member.HeroId, secondSettlementId,
                    new[] { new ItemRosterElement(Get<ItemObject>(instance, otherItemId), 3) });
                if (instance != Server)
                {
                    var ownerId = instance == leaderClient ? leader.HeroId : member.HeroId;
                    foreach (var id in new[] { settlementId, secondSettlementId })
                    {
                        var settlement = Get<Settlement>(instance, id);
                        behavior.AddNewWarehouseDataIfNeeded(settlement);
                        behavior.GetWarehouseRoster(settlement).Add(data.GetWarehouseRoster(ownerId, id));
                    }
                }
            });
        }
        Join();
        Server.Call(() => Server.Resolve<ISessionWorkshopPlayerDataInterface>().TransferWarehouseData(
            Get<Hero>(Server, member.HeroId), Get<Hero>(Server, leader.HeroId)));
        Send(memberClient, new RequestClanMemberLeave(member.HeroId, member.HeroId));
        Flush();

        Server.Call(() =>
        {
            var data = Server.Resolve<ISessionWorkshopPlayerDataInterface>();
            Assert.Equal(15, Assert.Single(data.GetWarehouseRoster(leader.HeroId, settlementId)).Amount);
            Assert.Equal(3, Assert.Single(data.GetWarehouseRoster(leader.HeroId, secondSettlementId)).Amount);
            Assert.Empty(data.GetWarehouseRoster(member.HeroId, settlementId));
            Assert.Empty(data.GetWarehouseRoster(member.HeroId, secondSettlementId));
        });
        leaderClient.Call(() =>
        {
            var behavior = Campaign.Current.GetCampaignBehavior<WorkshopsCampaignBehavior>();
            Assert.Equal(15, Assert.Single(behavior.GetWarehouseRoster(Get<Settlement>(leaderClient, settlementId))).Amount);
            Assert.Equal(3, Assert.Single(behavior.GetWarehouseRoster(Get<Settlement>(leaderClient, secondSettlementId))).Amount);
        });
        memberClient.Call(() => Assert.DoesNotContain(
            Campaign.Current.GetCampaignBehavior<WorkshopsCampaignBehavior>()._warehouseRosterPerSettlement,
            entry => entry.Key == Get<Settlement>(memberClient, settlementId) ||
                     entry.Key == Get<Settlement>(memberClient, secondSettlementId)));
    }

    // M02, M03, M06, M10: either arrangement keeps both parties and locks departure symmetrically.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PlayerMarriage_PreservesPartiesAndPreventsLeaveOrRemoval(bool matrilineal)
    {
        Server.Call(() =>
        {
            var first = Get<Hero>(Server, leader.HeroId);
            var second = Get<Hero>(Server, member.HeroId);
            first.IsFemale = false;
            second.IsFemale = true;
            Assert.True(Server.Resolve<IPlayerMarriageRules>().TryApply(first, second, matrilineal));
            Assert.False(Server.Resolve<IPlayerMarriageRules>().TryApply(first, second, matrilineal));
        });
        Flush();
        Send(memberClient, new RequestClanMemberLeave(member.HeroId, member.HeroId));
        Send(leaderClient, new RequestClanMemberLeave(leader.HeroId, member.HeroId));
        Send(leaderClient, new RequestClanMemberLeave(leader.HeroId, leader.HeroId));
        Send(memberClient, new RequestClanMemberLeave(member.HeroId, leader.HeroId));
        Flush();
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                var first = Get<Hero>(instance, leader.HeroId);
                var second = Get<Hero>(instance, member.HeroId);
                Assert.Same(first, second.Spouse);
                Assert.Same(second, first.Spouse);
                Assert.Same(first.Clan, second.Clan);
                Assert.Same(matrilineal ? second : first, first.Clan.Leader);
                Assert.Same(first, Get<MobileParty>(instance, leader.MobilePartyId).LeaderHero);
                Assert.Same(second, Get<MobileParty>(instance, member.MobilePartyId).LeaderHero);
            });
        }
    }

    // S01, S02, S17, S21: personal succession must preserve the controller, party and original clan.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PersonalSuccession_PreservesPlayerIdentityAndClanRole(bool leaderDies)
    {
        Join();
        var player = leaderDies ? leader : member;
        var client = leaderDies ? leaderClient : memberClient;
        var heirId = TestEnvironment.CreateRegisteredObject<Hero>();
        Server.Call(() =>
        {
            var original = Get<Hero>(Server, player.HeroId);
            var heir = Get<Hero>(Server, heirId);
            heir.Occupation = Occupation.Lord;
            heir.SetBirthDay(CampaignTime.YearsFromNow(-20));
            heir.Father = original;
            heir.Clan = original.Clan;
            Get<MobileParty>(Server, player.MobilePartyId).MemberRoster.AddToCounts(heir.CharacterObject, 1);
            Server.Resolve<IHeirSelectionCampaignBehaviorInterface>().PrepareSuccession(original);
            original.AddDeathMark(null, KillCharacterAction.KillCharacterActionDetail.DiedInBattle);
            original.ChangeState(Hero.CharacterStates.Dead);
        });
        Flush();
        Send(client, new NetworkHeirSelectionOver(player.HeroId, heirId));
        Flush();
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.Resolve<IPlayerManager>().TryGetPlayer(player.ControllerId, out var replacement));
                Assert.Equal(heirId, replacement.HeroId);
                Assert.Equal(player.MobilePartyId, replacement.MobilePartyId);
                Assert.Equal(player.OriginalClanId, replacement.OriginalClanId);
                Assert.Equal(leader.ClanId, replacement.ClanId);
                var heir = Get<Hero>(instance, heirId);
                Assert.Same(heir, Get<MobileParty>(instance, player.MobilePartyId).LeaderHero);
                Assert.Equal(leaderDies, heir.Clan.Leader == heir);
            });
        }
        if (!leaderDies)
        {
            Send(client, new RequestClanMemberLeave(heirId, heirId));
            Flush();
            foreach (var instance in Clients.Prepend(Server))
                instance.Call(() => Assert.Same(Get<Hero>(instance, heirId), Get<Clan>(instance, member.OriginalClanId).Leader));
        }
    }

    // S13, S14, S16: the leader has first choice and an already claimed heir cannot be reused.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void BothSpousesDie_LeaderChoosesFirstAndMemberCannotClaimTheSameHeir(int heirCount)
    {
        Join();
        var heirIds = Enumerable.Range(0, heirCount).Select(_ => TestEnvironment.CreateRegisteredObject<Hero>()).ToArray();
        Server.Call(() =>
        {
            var first = Get<Hero>(Server, leader.HeroId);
            var second = Get<Hero>(Server, member.HeroId);
            first.Spouse = second;
            second.Spouse = first;
            foreach (var heirId in heirIds)
            {
                var heir = Get<Hero>(Server, heirId);
                heir.Occupation = Occupation.Lord;
                heir.SetBirthDay(CampaignTime.YearsFromNow(-20));
                heir.Father = first;
                heir.Mother = second;
                heir.Clan = first.Clan;
            }
            Get<MobileParty>(Server, leader.MobilePartyId).MemberRoster.AddToCounts(Get<Hero>(Server, heirIds[0]).CharacterObject, 1);
            if (heirCount == 2)
                Get<MobileParty>(Server, member.MobilePartyId).MemberRoster.AddToCounts(Get<Hero>(Server, heirIds[1]).CharacterObject, 1);
            var succession = Server.Resolve<IHeirSelectionCampaignBehaviorInterface>();
            succession.PrepareSuccession(second);
            succession.PrepareSuccession(first);
            foreach (var hero in new[] { second, first })
            {
                hero.AddDeathMark(null, KillCharacterAction.KillCharacterActionDetail.DiedInBattle);
                hero.ChangeState(Hero.CharacterStates.Dead);
                hero.Spouse = null;
            }
        });
        Flush();
        Send(memberClient, new NetworkHeirSelectionOver(member.HeroId, heirIds[0]));
        Flush();
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(member.ControllerId, out var waiting));
            Assert.Equal(member.HeroId, waiting.HeroId);
            Assert.True(Server.NetworkSentMessages.GetMessages<NetworkClientSelectHeir>().Last().WaitingForHeirSelection);
        });

        Send(leaderClient, new NetworkHeirSelectionOver(leader.HeroId, heirIds[0]));
        Flush();
        Send(memberClient, new NetworkHeirSelectionOver(member.HeroId, heirIds[0]));
        Flush();
        Server.Call(() =>
        {
            Assert.Same(Get<Hero>(Server, heirIds[0]), Get<Clan>(Server, leader.ClanId).Leader);
            if (heirCount == 1)
            {
                Assert.False(Server.Resolve<IPlayerManager>().TryGetPlayer(member.ControllerId, out _));
                Assert.Contains(Server.NetworkSentMessages.GetMessages<NetworkClientGameOver>(),
                    message => message.PlayerHeroId == member.HeroId && message.ClanSurvives);
            }
            else
            {
                Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(member.ControllerId, out var waiting));
                Assert.Equal(member.HeroId, waiting.HeroId);
                var choices = Server.NetworkSentMessages.GetMessages<NetworkClientSelectHeir>().Last();
                Assert.True(choices.SelectionRejected);
                Assert.Equal(heirIds[1], Assert.Single(choices.HeirIdApparents).Key);
            }
        });
        if (heirCount == 2)
        {
            Send(memberClient, new NetworkHeirSelectionOver(member.HeroId, heirIds[1]));
            Flush();
        }
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                var manager = instance.Resolve<IPlayerManager>();
                Assert.True(manager.TryGetPlayer(leader.ControllerId, out var first));
                Assert.Equal(heirIds[0], first.HeroId);
                Assert.Null(Get<Hero>(instance, heirIds[0]).Spouse);
                if (heirCount == 2)
                {
                    Assert.True(manager.TryGetPlayer(member.ControllerId, out var second));
                    Assert.Equal(heirIds[1], second.HeroId);
                    Assert.Null(Get<Hero>(instance, heirIds[1]).Spouse);
                }
                else if (instance != memberClient)
                    Assert.False(manager.TryGetPlayer(member.ControllerId, out _));
                else
                    Assert.Single(instance.InternalMessages.GetMessages<NetworkClientGameOver>());
            });
        }
    }

    // S04, S06, S07, S08: appointment needs no successor confirmation; an offline leader gets a year.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LeaderWithoutHeirs_AppointsOfflineMemberOrWaitsForOfflineDeadline(bool leaderDisconnected)
    {
        Join();
        Server.Call(() =>
        {
            var original = Get<Hero>(Server, leader.HeroId);
            Server.Resolve<IHeirSelectionCampaignBehaviorInterface>().PrepareSuccession(original);
            original.AddDeathMark(null, KillCharacterAction.KillCharacterActionDetail.DiedInBattle);
            original.ChangeState(Hero.CharacterStates.Dead);
            var manager = Server.Resolve<IPlayerManager>();
            manager.ClearPeer(memberClient.NetPeer);
            if (leaderDisconnected) manager.ClearPeer(leaderClient.NetPeer);
            Server.Resolve<IMessageBroker>().Publish(this, new PlayerHeirSelectionRequested(original));
        });
        Flush();
        if (leaderDisconnected)
        {
            Server.Call(() =>
            {
                var original = Get<Hero>(Server, leader.HeroId);
                var state = Server.Resolve<IHeirSelectionCampaignBehaviorInterface>().GetSuccession(original);
                Assert.Equal(CampaignTime.YearsFromNow(1).ToDays, state.AppointmentDeadlineDays);
                Assert.Same(original, original.Clan.Leader);
                state.AppointmentDeadlineDays = CampaignTime.Now.ToDays - 1;
                Server.Resolve<IMessageBroker>().Publish(this, new PlayerHeirSelectionRequested(original));
            });
        }
        else
        {
            Server.Call(() =>
            {
                var choice = Server.NetworkSentMessages.GetMessages<NetworkClientSelectHeir>().Last();
                Assert.True(choice.AppointClanLeader);
                Assert.Equal(member.HeroId, Assert.Single(choice.HeirIdApparents).Key);
            });
            Send(leaderClient, new NetworkHeirSelectionOver(leader.HeroId, member.HeroId, true));
        }
        Flush();
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                var successor = Get<Hero>(instance, member.HeroId);
                Assert.Same(successor, Get<Clan>(instance, leader.ClanId).Leader);
                Assert.False(successor.Clan.IsEliminated);
                Assert.Same(successor, Get<MobileParty>(instance, member.MobilePartyId).LeaderHero);
                Assert.True(instance.Resolve<IPlayerManager>().TryGetPlayer(member.ControllerId, out var player));
                Assert.Equal(member.HeroId, player.HeroId);
            });
        }
    }

    private Player CreatePlayer(EnvironmentInstance client, string controllerId)
    {
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Player player = null!;
        Server.Call(() =>
        {
            var party = Get<MobileParty>(Server, partyId);
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out var heroId));
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero.Clan, out var clanId));
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero.CharacterObject, out var characterId));
            player = new Player(controllerId, heroId, partyId, clanId, characterId, clanId);
        });
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                using var scope = new AllowedThread();
                var hero = Get<Hero>(instance, player.HeroId);
                hero.Clan._banner = new Banner();
                hero.SetBirthDay(CampaignTime.YearsFromNow(-30));
                hero.Occupation = Occupation.Lord;
                hero.Gold = 10000;
                Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(player));
                if (instance == client)
                {
                    instance.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
                    Game.Current.PlayerTroop = hero.CharacterObject;
                }
            });
        }
        Server.Call(() => Server.Resolve<IPlayerManager>().SetPeer(controllerId, client.NetPeer));
        return player;
    }

    private void Join()
    {
        Server.Call(() => Server.Resolve<IClanJoinRules>().Apply(
            Get<Hero>(Server, member.HeroId), Get<Clan>(Server, leader.ClanId)));
        Flush();
    }

    private void Send<T>(EnvironmentInstance client, T message) where T : IMessage
        => client.Call(() => client.Resolve<INetwork>().SendAll(message));

    private void Flush()
    {
        Server.PumpGameThread();
        TestEnvironment.FlushCoalescer();
        foreach (var client in Clients) client.PumpGameThread();
    }

    private static T Get<T>(EnvironmentInstance instance, string id) where T : class
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var result));
        return result;
    }
}
