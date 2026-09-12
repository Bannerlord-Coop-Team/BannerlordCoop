using Autofac;
using Common.Messaging;
using GameInterface.Services.Locations.Conversations;
using GameInterface.Services.Locations.Conversations.Patches;
using GameInterface.Services.Locations.Messages.Conversation;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Services.Locations;

public sealed class LocationConversationPatchLifecycleTests
{
    private static readonly MethodInfo OnBehaviorInitializePostfix =
        AccessTools.Method(typeof(LocationConversationPatches), "OnBehaviorInitializePostfix");
    private static readonly MethodInfo OnEndMissionPostfix =
        AccessTools.Method(typeof(LocationConversationPatches), "OnEndMissionPostfix");

    [Fact]
    public void MissionLeave_ClearsPendingBeforeReentryAndPublishesRelease()
    {
        var state = new LocationConversationClientState();
        Assert.True(state.TryBeginPending(NewAgent(), "location", "before-leave", out var staleGeneration));

        var releases = InvokeLifecycle(state, OnEndMissionPostfix);

        Assert.Single(releases);
        Assert.False(state.HasPendingOrHeld);
        Assert.True(state.TryBeginPending(NewAgent(), "location", "after-reentry", out var currentGeneration));

        LocationConversationPatches.StartApprovedConversation(state, staleGeneration);
        Assert.False(LocationConversationPatches.CancelPending(state, staleGeneration));
        Assert.True(state.TryTakePending(currentGeneration, out var pending));
        Assert.Equal("after-reentry", pending.CharacterId);
    }

    [Fact]
    public void MissionReentry_ClearsHeldStateOnlyOnce()
    {
        var state = new LocationConversationClientState();
        state.Hold("location|held");

        var releases = InvokeLifecycle(state, OnBehaviorInitializePostfix, invokeTwice: true);

        Assert.Single(releases);
        Assert.False(state.HasPendingOrHeld);
        Assert.Null(state.HeldNpcKey);
    }

    private static IReadOnlyList<LocationConversationEnded> InvokeLifecycle(
        LocationConversationClientState state,
        MethodInfo lifecycleMethod,
        bool invokeTwice = false)
    {
        var releases = new List<LocationConversationEnded>();
        Action<MessagePayload<LocationConversationEnded>> capture = payload => releases.Add(payload.What);
        bool hadPreviousContainer = ContainerProvider.TryGetContainer(out var previousContainer);
        var builder = new ContainerBuilder();
        builder.RegisterInstance(state).As<ILocationConversationClientState>();
        using var container = builder.Build();

        MessageBroker.Instance.Subscribe(capture);
        try
        {
            ContainerProvider.SetContainer(container);
            lifecycleMethod.Invoke(null, null);
            if (invokeTwice)
                lifecycleMethod.Invoke(null, null);
        }
        finally
        {
            MessageBroker.Instance.Unsubscribe(capture);
            if (hadPreviousContainer)
                ContainerProvider.SetContainer(previousContainer);
            else
                ContainerProvider.Clear();
        }

        return releases;
    }

    private static Agent NewAgent() =>
        (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
}
