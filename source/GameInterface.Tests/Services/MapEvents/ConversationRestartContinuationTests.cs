using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Services.SiegeEvents;
using HarmonyLib;
using Moq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

[Collection(nameof(CampaignCurrentCollection))]
public class ConversationRestartContinuationTests : IDisposable
{
    private readonly bool previousServer = ModInformation.IsServer;
    private readonly Harmony harmony = new("test.poachers.restart-continuation");
    private readonly MessageBroker messages = new();
    private readonly Mock<INetwork> network = new();
    private readonly RestartTracker tracker = new();
    private readonly PartyBase defender = ObjectHelper.SkipConstructor<PartyBase>();
    private readonly PartyBase attacker = ObjectHelper.SkipConstructor<PartyBase>();
    private readonly ConversationRequestHandler handler;
    private readonly List<bool> completions = new();

    public ConversationRestartContinuationTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
        GameBootStrap.Initialize();
        ModInformation.IsServer = false;
        PlayerPartyInteractionDialogState.Clear();
        var objects = new Mock<IObjectManager>();
        var defenderId = "defender";
        var attackerId = "attacker";
        objects.Setup(manager => manager.TryGetIdWithLogging(defender, out defenderId)).Returns(true);
        objects.Setup(manager => manager.TryGetIdWithLogging(attacker, out attackerId)).Returns(true);
        var resolvedDefender = defender;
        var resolvedAttacker = attacker;
        objects.Setup(manager => manager.TryGetObjectWithLogging(defenderId, out resolvedDefender)).Returns(true);
        objects.Setup(manager => manager.TryGetObjectWithLogging(attackerId, out resolvedAttacker)).Returns(true);
        handler = new ConversationRequestHandler(messages, network.Object, objects.Object, null!, tracker, null!, null!);
        harmony.Patch(AccessTools.Method(typeof(PlayerEncounter), nameof(PlayerEncounter.RestartPlayerEncounter)),
            prefix: new HarmonyMethod(typeof(ConversationRestartContinuationTests), nameof(SkipNativeRestart)));
    }

    public void Dispose()
    {
        handler.Dispose();
        messages.Dispose();
        harmony.UnpatchAll(harmony.Id);
        ModInformation.IsServer = previousServer;
    }

    private static bool SkipNativeRestart()
    {
        Assert.True(AllowedThread.IsThisThreadAllowed());
        return false;
    }

    private void Request()
    {
        messages.Publish(this, new ConversationRequested(defender, attacker, false,
            ConversationRestartSource.PlayerEncounter, false, accepted =>
            {
                Assert.False(AllowedThread.IsThisThreadAllowed());
                completions.Add(accepted);
            }));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void ApprovalResolvesContinuationOnceOutsideReceiveScope(int decision, bool accepted)
    {
        tracker.Decision = (ConversationRestartDecision)decision;

        GameThread.Run(() =>
        {
            Request();
            var approval = new NetworkAllowConversation("defender", "attacker", false,
                ConversationRestartSource.PlayerEncounter, "request");
            messages.Publish(this, approval);
            messages.Publish(this, approval);
        }, blocking: true);
        GameThread.Run(() => { }, blocking: true);

        Assert.Equal(accepted, Assert.Single(completions));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EndingOrDisposingPendingEncounterCancelsContinuation(bool dispose)
    {
        GameThread.Run(() =>
        {
            Request();
            if (dispose) handler.Dispose();
            else messages.Publish(this, new ConversationEnded());
        }, blocking: true);

        Assert.False(Assert.Single(completions));
    }

    [Fact]
    public void FailedSendCancelsContinuation()
    {
        network.Setup(connection => connection.SendAll(It.IsAny<IMessage>())).Throws(new InvalidOperationException("send failed"));

        GameThread.Run(Request, blocking: true);

        Assert.False(Assert.Single(completions));
        Assert.Equal("request", Assert.Single(tracker.Removed));
    }

    private class RestartTracker : IConversationRestartContextTracker
    {
        public ConversationRestartDecision Decision;
        public readonly List<string> Removed = new();
        public string Capture(PlayerEncounter encounter) => "request";
        public ConversationRestartDecision Consume(string requestId, PlayerEncounter encounter, PartyBase defender, PartyBase attacker) => Decision;
        public void Remove(string requestId) => Removed.Add(requestId);
    }
}
