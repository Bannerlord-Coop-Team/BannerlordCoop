using System;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Surrogates;
using Missions.Battles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// The host fields a NEW NPC party that joins a live coop battle through our own spawn path: on the
/// involved-parties broadcast, <c>ReinforcementFielder.Handle_ReinforcementPartiesAdded</c> spawns the AI
/// party's troops into the mission. Runs headless against the <see cref="MissionEngineFixture"/> mock mission
/// (extended here with per-side teams). Also verifies the gating: it only fields a new AI party when the local
/// client is the host AND the battle is activated, queues parties that arrive before activation, and never
/// re-fields the same party.
/// </summary>
public class BattleReinforcementSpawnTests : MissionTestEnvironment
{
    public BattleReinforcementSpawnTests(ITestOutputHelper output) : base(output) { }

    /// <summary>Add an AI party (registered on every instance, no player owner) to the battle's defender side and
    /// give it a spawnable roster ON THE HOST (the headless mock spawn needs a real character). Returns its
    /// MapEventParty id.</summary>
    private string AddAiReinforcementParty(string mapEventId, EnvironmentInstance host)
    {
        var aiPartyId = CreateRegisteredObject<MobileParty>();
        string mapEventPartyId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(aiPartyId, out var aiParty));

            aiParty.Party.MapEventSide = mapEvent.DefenderSide;

            var mep = mapEvent.DefenderSide.Parties.Last(p => p.Party == aiParty.Party);
            Assert.True(Server.ObjectManager.TryGetId(mep, out mapEventPartyId));
        }, MapEventDisabledMethods);

        host.Call(() =>
        {
            Assert.True(host.ObjectManager.TryGetObject<MobileParty>(aiPartyId, out var aiParty));
            aiParty.Party.MemberRoster.Clear();
            aiParty.Party.MemberRoster.AddToCounts((CharacterObject)Game.Current.PlayerTroop, 3);
        });

        Assert.NotNull(mapEventPartyId);
        return mapEventPartyId;
    }

    private static void PublishInvolvedPartiesAdded(EnvironmentInstance instance, string mapEventId, string mapEventPartyId)
    {
        instance.Resolve<IMessageBroker>().Publish(instance,
            new NetworkAddInvolvedParties(mapEventId, new[] { mapEventPartyId }, new[] { new CampaignVec2(default, true) }));
    }

    [Fact]
    public void HostActivated_NewAiParty_IsFieldedIntoTheMission()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var host = Clients.First();
        var aiMapEventPartyId = AddAiReinforcementParty(mapEventId, host);

        CoopBattleController controller = null;
        MockMission mock = null;
        host.Call(() =>
        {
            mock = fixture.CreateMission(host);
            controller = host.Resolve<CoopBattleController>();
        });

        EnterBattle(host, mapEventId);          // elect "host" and set the controller's instance id

        host.Call(() =>
        {
            controller.OnDeploymentFinished();  // host commits -> battle activated
            PublishInvolvedPartiesAdded(host, mapEventId, aiMapEventPartyId);

            Assert.True(mock.Agents.Count > 0, "the host should have fielded the new AI party's troops");
        });

        GC.KeepAlive(controller);
    }

    [Fact]
    public void PartyAddedBeforeActivation_IsFieldedAfterActivation()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var host = Clients.First();
        var aiMapEventPartyId = AddAiReinforcementParty(mapEventId, host);

        CoopBattleController controller = null;
        MockMission mock = null;
        host.Call(() =>
        {
            mock = fixture.CreateMission(host);
            controller = host.Resolve<CoopBattleController>();
        });

        EnterBattle(host, mapEventId);          // elected host, but deployment NOT committed -> not activated

        host.Call(() =>
        {
            PublishInvolvedPartiesAdded(host, mapEventId, aiMapEventPartyId);
            Assert.Empty(mock.Agents);

            controller.OnDeploymentFinished();
            controller.OnMissionTick(0f);

            Assert.True(mock.Agents.Count > 0, "the queued AI party should be fielded after activation");
        });

        GC.KeepAlive(controller);
    }

    [Fact]
    public void DuplicateInvolvedPartiesBroadcast_FieldsTheAiPartyOnce()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "client");
        var host = Clients.First();
        var aiMapEventPartyId = AddAiReinforcementParty(mapEventId, host);

        CoopBattleController controller = null;
        MockMission mock = null;
        host.Call(() =>
        {
            mock = fixture.CreateMission(host);
            controller = host.Resolve<CoopBattleController>();
        });

        EnterBattle(host, mapEventId);

        host.Call(() =>
        {
            controller.OnDeploymentFinished();
            PublishInvolvedPartiesAdded(host, mapEventId, aiMapEventPartyId);
            int afterFirst = mock.Agents.Count;
            Assert.True(afterFirst > 0, "the host should have fielded the new AI party's troops");

            PublishInvolvedPartiesAdded(host, mapEventId, aiMapEventPartyId);
            Assert.Equal(afterFirst, mock.Agents.Count); // fielded once, not twice
        });

        GC.KeepAlive(controller);
    }
}
