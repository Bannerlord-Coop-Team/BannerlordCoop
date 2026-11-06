#if COOP_NAVAL
using Common;
using Common.Messaging;
using Common.Network;
using Common.Tests.Utils;
using Coop.Naval.CampaignBehaviors.Handlers;
using Coop.Naval.CampaignBehaviors.Interfaces;
using Coop.Naval.CampaignBehaviors.Messages;
using Coop.Naval.CampaignBehaviors.Patches;
using ObjectManager = GameInterface.Services.ObjectManager.ObjectManager;
using Moq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace Coop.Tests.Naval;

[Collection(ModInformationRoleCollection.Name)]
public class NavalPatrolPartiesSyncTests : IDisposable
{
    private const uint SettlementHandle = 11;
    private const uint PatrolHandle = 12;

    private readonly bool wasServer = ModInformation.IsServer;
    private readonly TestMessageBroker messageBroker = new TestMessageBroker();
    private readonly ObjectManager objectManager = new(Mock.Of<ILogger>());
    private readonly Mock<INetwork> network = new Mock<INetwork>();
    private readonly Mock<INavalPatrolPartiesCampaignBehaviorInterface> patrolInterface = new Mock<INavalPatrolPartiesCampaignBehaviorInterface>();
    private readonly NavalPatrolPartiesCampaignBehavior behavior = (NavalPatrolPartiesCampaignBehavior)FormatterServices.GetUninitializedObject(typeof(NavalPatrolPartiesCampaignBehavior));
    private readonly Settlement settlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
    private readonly MobileParty patrol = CreateNavalPatrol();
    private readonly List<NavalPatrolSettlementChanged> published = new List<NavalPatrolSettlementChanged>();
    private readonly NavalPatrolPartiesHandler handler;

    public NavalPatrolPartiesSyncTests()
    {
        behavior._partyGenerationQueue = new Dictionary<Settlement, CampaignTime>();
        behavior._patrolParties = new Dictionary<Settlement, MobileParty>();
        ((PatrolPartyComponent)patrol._partyComponent)._homeSettlement = settlement;

        handler = new NavalPatrolPartiesHandler(messageBroker, objectManager, network.Object, patrolInterface.Object);
        MessageBroker.Instance.Subscribe<NavalPatrolSettlementChanged>(Receive);
    }

    public void Dispose()
    {
        MessageBroker.Instance.Unsubscribe<NavalPatrolSettlementChanged>(Receive);
        handler.Dispose();
        ModInformation.IsServer = wasServer;
    }

    private void Receive(MessagePayload<NavalPatrolSettlementChanged> payload) => published.Add(payload.What);

    [Fact]
    public void DailyTickSettlement_Client_DoesNotRun()
    {
        ModInformation.IsServer = false;

        Assert.False(NavalPatrolPartiesCampaignBehaviorPatches.DailyTickSettlementPrefix(behavior, settlement, out _));
    }

    // A town starting to train a patrol shows "Ready in N days" on clients
    [Fact]
    public void DailyTickSettlement_Queued_PublishesTheQueueTime()
    {
        ModInformation.IsServer = true;
        var queueTime = new CampaignTime(5000L);

        Assert.True(NavalPatrolPartiesCampaignBehaviorPatches.DailyTickSettlementPrefix(behavior, settlement, out var before));
        behavior._partyGenerationQueue[settlement] = queueTime;
        NavalPatrolPartiesCampaignBehaviorPatches.DailyTickSettlementPostfix(behavior, settlement, before);

        var state = Assert.Single(published);
        Assert.Same(settlement, state.Settlement);
        Assert.True(state.IsQueued);
        Assert.Equal(queueTime, state.QueueTime);
        Assert.Null(state.PatrolParty);
    }

    // A client that joined during training gets the spawned patrol in place of the queue entry
    [Fact]
    public void DailyTickSettlement_Spawned_PublishesThePatrol()
    {
        ModInformation.IsServer = true;
        behavior._partyGenerationQueue[settlement] = new CampaignTime(5000L);

        Assert.True(NavalPatrolPartiesCampaignBehaviorPatches.DailyTickSettlementPrefix(behavior, settlement, out var before));
        behavior._partyGenerationQueue.Remove(settlement);
        behavior._patrolParties[settlement] = patrol;
        NavalPatrolPartiesCampaignBehaviorPatches.DailyTickSettlementPostfix(behavior, settlement, before);

        var state = Assert.Single(published);
        Assert.False(state.IsQueued);
        Assert.Same(patrol, state.PatrolParty);
    }

    [Fact]
    public void DailyTickSettlement_Unchanged_PublishesNothing()
    {
        ModInformation.IsServer = true;
        behavior._partyGenerationQueue[settlement] = new CampaignTime(5000L);

        Assert.True(NavalPatrolPartiesCampaignBehaviorPatches.DailyTickSettlementPrefix(behavior, settlement, out var before));
        NavalPatrolPartiesCampaignBehaviorPatches.DailyTickSettlementPostfix(behavior, settlement, before);

        Assert.Empty(published);
    }

    // A patrol from the join save is removed on clients once the server destroys it
    [Fact]
    public void OnMobilePartyDestroyed_NavalPatrol_PublishesTheRemoval()
    {
        ModInformation.IsServer = true;

        NavalPatrolPartiesCampaignBehaviorPatches.OnMobilePartyDestroyedPostfix(behavior, patrol);

        var state = Assert.Single(published);
        Assert.Same(settlement, state.Settlement);
        Assert.False(state.IsQueued);
        Assert.Null(state.PatrolParty);
    }

    [Fact]
    public void OnMobilePartyDestroyed_Client_PublishesNothing()
    {
        ModInformation.IsServer = false;

        NavalPatrolPartiesCampaignBehaviorPatches.OnMobilePartyDestroyedPostfix(behavior, patrol);

        Assert.Empty(published);
    }

    [Fact]
    public void NavalPatrolSettlementChanged_SendsHandlesToClients()
    {
        RegisterSettlementAndPatrol();
        var queueTime = new CampaignTime(5000L);

        messageBroker.Publish(this, new NavalPatrolSettlementChanged(settlement, true, queueTime, patrol));

        network.Verify(net => net.SendAll(It.Is<NetworkNavalPatrolSettlementChanged>(message =>
            message.SettlementHandle == SettlementHandle &&
            message.IsQueued &&
            message.QueueTime == queueTime &&
            message.PatrolPartyHandle == PatrolHandle)), Times.Once);
    }

    [Fact]
    public void NavalPatrolSettlementChanged_NoPatrol_SendsAZeroHandle()
    {
        Assert.True(objectManager.AddExisting("town_ES1", settlement, SettlementHandle));

        messageBroker.Publish(this, new NavalPatrolSettlementChanged(settlement, false, CampaignTime.Zero, null));

        network.Verify(net => net.SendAll(It.Is<NetworkNavalPatrolSettlementChanged>(message =>
            message.SettlementHandle == SettlementHandle && message.PatrolPartyHandle == 0)), Times.Once);
    }

    [Fact]
    public void NavalPatrolSettlementChanged_UnregisteredPatrol_SendsNothing()
    {
        Assert.True(objectManager.AddExisting("town_ES1", settlement, SettlementHandle));

        messageBroker.Publish(this, new NavalPatrolSettlementChanged(settlement, false, CampaignTime.Zero, patrol));

        network.Verify(net => net.SendAll(It.IsAny<IMessage>()), Times.Never);
    }

    [Fact]
    public void NetworkNavalPatrolSettlementChanged_AppliesTheResolvedState()
    {
        RegisterSettlementAndPatrol();
        var queueTime = new CampaignTime(5000L);

        messageBroker.Publish(this, new NetworkNavalPatrolSettlementChanged(SettlementHandle, true, queueTime, PatrolHandle));
        DrainGameThread();

        patrolInterface.Verify(patrols => patrols.SetSettlementState(settlement, true, queueTime, patrol), Times.Once);
    }

    [Fact]
    public void NetworkNavalPatrolSettlementChanged_ZeroHandle_RemovesThePatrol()
    {
        Assert.True(objectManager.AddExisting("town_ES1", settlement, SettlementHandle));

        messageBroker.Publish(this, new NetworkNavalPatrolSettlementChanged(SettlementHandle, false, CampaignTime.Zero, 0));
        DrainGameThread();

        patrolInterface.Verify(patrols => patrols.SetSettlementState(settlement, false, CampaignTime.Zero, null), Times.Once);
    }

    [Fact]
    public void NetworkNavalPatrolSettlementChanged_UnknownPatrol_AppliesNothing()
    {
        Assert.True(objectManager.AddExisting("town_ES1", settlement, SettlementHandle));

        messageBroker.Publish(this, new NetworkNavalPatrolSettlementChanged(SettlementHandle, false, CampaignTime.Zero, PatrolHandle));
        DrainGameThread();

        patrolInterface.Verify(patrols => patrols.SetSettlementState(
            It.IsAny<Settlement>(), It.IsAny<bool>(), It.IsAny<CampaignTime>(), It.IsAny<MobileParty>()), Times.Never);
    }

    [Fact]
    public void Dispose_UnsubscribesBothMessages()
    {
        handler.Dispose();

        Assert.Equal(-1, messageBroker.GetSubscriberCountForType<NavalPatrolSettlementChanged>());
        Assert.Equal(-1, messageBroker.GetSubscriberCountForType<NetworkNavalPatrolSettlementChanged>());
    }

    private void RegisterSettlementAndPatrol()
    {
        Assert.True(objectManager.AddExisting("town_ES1", settlement, SettlementHandle));
        Assert.True(objectManager.AddExisting("naval_patrol_party_1", patrol, PatrolHandle));
    }

    private static MobileParty CreateNavalPatrol()
    {
        var component = (PatrolPartyComponent)FormatterServices.GetUninitializedObject(typeof(PatrolPartyComponent));
        component.IsNaval = true;

        var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        party._partyComponent = component;
        party.IsPatrolParty = true;
        return party;
    }

    private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);
}
#endif
