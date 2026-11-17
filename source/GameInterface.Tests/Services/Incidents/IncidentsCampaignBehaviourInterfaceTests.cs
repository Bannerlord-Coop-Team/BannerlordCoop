using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Incidents.Interfaces;
using GameInterface.Services.Incidents.Messages;
using GameInterface.Services.Missions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Moq;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Incidents;
using TaleWorlds.CampaignSystem.Party;
using Xunit;
using IncidentTrigger = TaleWorlds.CampaignSystem.Incidents.Incident.IncidentTrigger;
using TriggerId = TaleWorlds.CampaignSystem.CampaignBehaviors.IncidentsCampaignBehaviour.IncidentTrigger;

namespace GameInterface.Tests.Services.Incidents;

public class IncidentsCampaignBehaviourInterfaceTests
{
    private const string ArrowProofingId = "Incident_incident_arrow_proofing";

    private readonly Player player = new Player("Controller_Player1", "Hero_Player1", "Party_Player1", "Clan_Player1", "Character_Player1");
    private readonly Hero playerHero = ObjectHelper.SkipConstructor<Hero>();
    private readonly MobileParty playerParty = ObjectHelper.SkipConstructor<MobileParty>();

    private readonly Mock<IPlayerManager> playerManager = new();
    private readonly Mock<ISessionIncidentsPlayerDataInterface> sessionData = new();
    private readonly Mock<IObjectManager> objectManager = new();
    private readonly Mock<INetwork> network = new();
    private readonly Mock<IMissionMembershipRegistry> missionMembership = new();
    private readonly List<IMessage> sentMessages = new();

    private readonly IncidentsCampaignBehaviourInterface incidentsInterface;

    public IncidentsCampaignBehaviourInterfaceTests()
    {
        // Not in a battle, since MobileParty.MapEvent reads it from the party base
        playerParty.Party = ObjectHelper.SkipConstructor<PartyBase>();

        NetPeer peer = null;
        playerManager.Setup(manager => manager.TryGetPeer(player.ControllerId, out peer)).Returns(true);

        network.Setup(n => n.Send(It.IsAny<NetPeer>(), It.IsAny<IMessage>()))
            .Callback<NetPeer, IMessage>((_, message) => sentMessages.Add(message));

        incidentsInterface = new IncidentsCampaignBehaviourInterface(
            playerManager.Object,
            sessionData.Object,
            objectManager.Object,
            network.Object,
            missionMembership.Object);
    }

    [Fact]
    public void ResendPendingIncident_SendsPendingIncidentWithItsStoredSeed()
    {
        string pendingIncidentId = ArrowProofingId;
        sessionData.Setup(data => data.TryGetPendingIncident(player.HeroId, out pendingIncidentId)).Returns(true);
        sessionData.Setup(data => data.GetActiveIncidentSeed(player.HeroId)).Returns(4242L);

        incidentsInterface.ResendPendingIncident(player);

        var message = Assert.IsType<NetworkIncidentInvoked>(Assert.Single(sentMessages));
        Assert.Equal(ArrowProofingId, message.IncidentId);
        Assert.Equal(4242L, message.ActiveIncidentSeed);
    }

    [Fact]
    public void ResendPendingIncident_NoPendingIncident_SendsNothing()
    {
        incidentsInterface.ResendPendingIncident(player);

        Assert.Empty(sentMessages);
    }

    [Fact]
    public void TryInvokeIncident_PendingIncident_DoesNotRoll()
    {
        string pendingIncidentId = ArrowProofingId;
        sessionData.Setup(data => data.TryGetPendingIncident(player.HeroId, out pendingIncidentId)).Returns(true);

        incidentsInterface.TryInvokeIncident(player, playerParty, playerHero, new IncidentTrigger(TriggerId.DuringSiege));

        // A roll would replace the stored seed the pending incident is resent with
        sessionData.Verify(data => data.SetActiveIncidentSeed(It.IsAny<string>(), It.IsAny<long>()), Times.Never);
        sessionData.Verify(data => data.IsOnGlobalCooldown(It.IsAny<string>()), Times.Never);
        Assert.Empty(sentMessages);
    }

    [Fact]
    public void TryInvokeIncident_LeavingBattleWhileInMission_DoesNotRollYet()
    {
        // The held roll running later comes from OnHourlyTick, which needs ContainerProvider-resolved player parties
        missionMembership.Setup(registry => registry.IsControllerInMission(player.ControllerId)).Returns(true);

        incidentsInterface.TryInvokeIncident(player, playerParty, playerHero, new IncidentTrigger(TriggerId.LeavingBattle));

        sessionData.Verify(data => data.TryGetPendingIncident(It.IsAny<string>(), out It.Ref<string>.IsAny), Times.Never);
        sessionData.Verify(data => data.SetActiveIncidentSeed(It.IsAny<string>(), It.IsAny<long>()), Times.Never);
        Assert.Empty(sentMessages);
    }

    [Fact]
    public void TryInvokeIncident_DisconnectedPlayer_DoesNotRoll()
    {
        playerManager.Setup(manager => manager.IsOwnerOfHeroDisconnected(playerHero)).Returns(true);

        incidentsInterface.TryInvokeIncident(player, playerParty, playerHero, new IncidentTrigger(TriggerId.DuringSiege));

        sessionData.Verify(data => data.SetActiveIncidentSeed(It.IsAny<string>(), It.IsAny<long>()), Times.Never);
        Assert.Empty(sentMessages);
    }

    [Fact]
    public void TryDebugInvokeIncident_PendingIncident_FailsWithoutSending()
    {
        string pendingIncidentId = ArrowProofingId;
        sessionData.Setup(data => data.TryGetPendingIncident(player.HeroId, out pendingIncidentId)).Returns(true);
        var incident = ObjectHelper.SkipConstructor<Incident>();

        bool invoked = incidentsInterface.TryDebugInvokeIncident(player, playerParty, playerHero, incident, out var error);

        Assert.False(invoked);
        Assert.Contains(ArrowProofingId, error);
        sessionData.Verify(data => data.SetPendingIncident(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        Assert.Empty(sentMessages);
    }
}
