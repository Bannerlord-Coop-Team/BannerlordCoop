using E2E.Tests.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEventSides.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>Verifies party removal when peace changes an active battle.</summary>
public class MapEventPeaceTests : MapEventTestBase
{
    public MapEventPeaceTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(BattleSideEnum.Attacker, false)]
    [InlineData(BattleSideEnum.Defender, false)]
    [InlineData(BattleSideEnum.Attacker, true)]
    [InlineData(BattleSideEnum.Defender, true)]
    public void ServerRemovesParty_ClientsReleasePartyWhileBattleContinues(BattleSideEnum side, bool removeLeader)
    {
        var context = CreateServerMapEvent();
        var reinforcementId = JoinNewServerPartyToSide(context.MapEventId, side);
        var removedId = removeLeader
            ? side == BattleSideEnum.Attacker ? context.AttackerPartyId : context.DefenderPartyId
            : reinforcementId;
        Server.NetworkSentMessages.Clear();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(removedId, out var party));
            Assert.NotNull(party.MapEvent);
            party.Party.MapEventSide = null;
            Assert.Null(party.MapEvent);
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));
            Assert.False(mapEvent.IsFinalized);
        }, MapEventDisabledMethods);

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkRemoveMapEventParty>());
        AssertReleasedOnClients(context.MapEventId, side, removedId);
    }

    [Theory]
    [InlineData(BattleSideEnum.Attacker)]
    [InlineData(BattleSideEnum.Defender)]
    public void ServerRemovesArmyLeader_ClientsReleaseAttachedMember(BattleSideEnum side)
    {
        var context = CreateServerMapEvent();
        var leaderId = JoinNewServerPartyToSide(context.MapEventId, side);
        string? memberId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(leaderId, out var leader));
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var member = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var army = new Army(kingdom, leader, Army.ArmyTypes.Raider);
            member.Army = army;
            member.AttachedTo = leader;
            Assert.Same(leader.MapEvent, member.MapEvent);
            Assert.True(Server.ObjectManager.TryGetId(member, out memberId));
        }, MapEventDisabledMethods);
        Server.NetworkSentMessages.Clear();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(leaderId, out var leader));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(memberId, out var member));
            leader.Party.MapEventSide = null;
            Assert.Null(leader.MapEvent);
            Assert.Null(member.MapEvent);
            Assert.Same(leader, member.AttachedTo);
        }, MapEventDisabledMethods);

        Assert.Equal(2, Server.NetworkSentMessages.GetMessages<NetworkRemoveMapEventParty>().Count());
        AssertReleasedOnClients(context.MapEventId, side, leaderId, memberId!);
    }

    [Theory]
    [InlineData(BattleSideEnum.Attacker)]
    [InlineData(BattleSideEnum.Defender)]
    public void ServerMakesPeace_ClientsReleaseDepartingFaction(BattleSideEnum reinforcementSide)
    {
        var context = CreateServerMapEvent();
        var reinforcementId = JoinNewServerPartyToSide(context.MapEventId, reinforcementSide);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(context.AttackerPartyId, out var attacker));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(context.DefenderPartyId, out var defender));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(reinforcementId, out var reinforcement));
            foreach (var participant in new[] { attacker, defender, reinforcement })
            {
                participant.ActualClan.Kingdom = Kingdom.CreateKingdom(Guid.NewGuid().ToString());
                participant.IsActive = true;
            }
            FactionManager.DeclareWar(attacker.MapFaction, defender.MapFaction);
            var opposingParty = reinforcementSide == BattleSideEnum.Attacker ? defender : attacker;
            FactionManager.DeclareWar(reinforcement.MapFaction, opposingParty.MapFaction);
            var ally = reinforcementSide == BattleSideEnum.Attacker ? attacker : defender;
            FactionManager.SetNeutral(reinforcement.MapFaction, ally.MapFaction);
            new PartyDiplomaticHandlerCampaignBehavior().RegisterEvents();
            Assert.True(attacker.Party.IsActive);
            Assert.True(defender.Party.IsActive);
            Assert.True(reinforcement.Party.IsActive);
            Assert.True(attacker.MapFaction.IsAtWarWith(defender.MapFaction));
            Assert.True(reinforcement.MapFaction.IsAtWarWith(opposingParty.MapFaction));
            Assert.False(mapEvent.ContainsPlayerParty());
            Assert.All(mapEvent.AttackerSide.Parties, entry =>
            {
                Assert.True(entry.Party.IsActive, "attacker side participant inactive");
                Assert.False(entry.Party.MapFaction.IsAtWarWith(attacker.MapFaction), "attacker side contains enemy faction");
            });
            Assert.All(mapEvent.DefenderSide.Parties, entry =>
            {
                Assert.True(entry.Party.IsActive, "defender side participant inactive");
                Assert.True(entry.Party.MapFaction.IsAtWarWith(attacker.MapFaction), "defender side contains neutral faction");
            });
            Assert.True(mapEvent.CanPartyJoinBattle(attacker.Party, BattleSideEnum.Attacker));

            var peacePartner = reinforcementSide == BattleSideEnum.Attacker ? defender : reinforcement;
            MakePeaceAction.Apply(attacker.MapFaction, peacePartner.MapFaction);

            Assert.False(FactionManager.IsAtWarAgainstFaction(attacker.MapFaction, peacePartner.MapFaction));
            Assert.Null(attacker.MapEvent);
            Assert.Equal(reinforcementSide == BattleSideEnum.Defender, mapEvent.IsFinalized);
        }, MapEventDisabledMethods);

        if (reinforcementSide == BattleSideEnum.Attacker)
        {
            AssertReleasedOnClients(context.MapEventId, BattleSideEnum.Attacker, context.AttackerPartyId);
        }
        else
        {
            foreach (var client in Clients)
            {
                client.Call(() =>
                {
                    Assert.False(client.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out _));
                    foreach (var partyId in new[] { context.AttackerPartyId, context.DefenderPartyId, reinforcementId })
                    {
                        Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                        Assert.Null(party.MapEvent);
                    }
                });
            }
        }
    }

    private void AssertReleasedOnClients(string mapEventId, BattleSideEnum side, params string[] partyIds)
    {
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
                Assert.NotEmpty(mapEvent.AttackerSide.Parties);
                Assert.NotEmpty(mapEvent.DefenderSide.Parties);
                foreach (var partyId in partyIds)
                {
                    Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                    Assert.Null(party.MapEvent);
                    Assert.DoesNotContain(mapEvent.GetMapEventSide(side).Parties, entry => entry.Party == party.Party);
                }
            });
        }
    }
}
