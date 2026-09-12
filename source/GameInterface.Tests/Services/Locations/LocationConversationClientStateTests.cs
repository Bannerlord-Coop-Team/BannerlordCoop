using GameInterface.Services.Locations.Conversations;
using GameInterface.Services.Locations.Conversations.Patches;
using System.Runtime.Serialization;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Services.Locations;

public sealed class LocationConversationClientStateTests
{
    [Fact]
    public void PendingAndHeldState_BlockCompetingRequestsUntilCleared()
    {
        var state = new LocationConversationClientState();
        var firstAgent = NewAgent();
        var secondAgent = NewAgent();

        Assert.True(state.TryBeginPending(firstAgent, "location", "first", out var generation));
        Assert.Equal(1, generation);
        Assert.False(state.TryBeginPending(secondAgent, "location", "second", out _));

        Assert.True(state.TryTakePending(generation, out var pending));
        Assert.Same(firstAgent, pending.Agent);
        Assert.Equal("location", pending.LocationId);
        Assert.Equal("first", pending.CharacterId);
        Assert.Equal(generation, pending.Generation);

        state.Hold("location|first");
        Assert.Equal("location|first", state.HeldNpcKey);
        Assert.False(state.TryBeginPending(secondAgent, "location", "second", out _));

        Assert.True(state.Clear());
        Assert.False(state.HasPendingOrHeld);
        Assert.Null(state.HeldNpcKey);
        Assert.False(state.Clear());
    }

    [Fact]
    public void StaleAllowAndDenyGenerations_DoNotMutateCurrentPendingRequest()
    {
        var state = new LocationConversationClientState();
        Assert.True(state.TryBeginPending(NewAgent(), "location", "first", out var staleGeneration));
        Assert.True(state.Clear());
        Assert.True(state.TryBeginPending(NewAgent(), "location", "second", out var currentGeneration));

        LocationConversationPatches.StartApprovedConversation(state, staleGeneration);
        Assert.False(LocationConversationPatches.CancelPending(state, staleGeneration));
        Assert.True(state.HasPendingOrHeld);

        Assert.True(state.TryTakePending(currentGeneration, out var pending));
        Assert.Equal("second", pending.CharacterId);
        Assert.False(state.HasPendingOrHeld);
    }

    [Fact]
    public void Clear_PreservesMonotonicGenerationAcrossMissionReentry()
    {
        var state = new LocationConversationClientState();

        Assert.True(state.TryBeginPending(NewAgent(), "location", "before-leave", out var beforeLeave));
        Assert.True(state.Clear());
        Assert.True(state.TryBeginPending(NewAgent(), "location", "after-reentry", out var afterReentry));

        Assert.Equal(beforeLeave + 1, afterReentry);
    }

    private static Agent NewAgent() =>
        (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
}
