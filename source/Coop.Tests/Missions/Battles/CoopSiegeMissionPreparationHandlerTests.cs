using System;
using System.Runtime.Serialization;
using GameInterface.Utils;
using Missions.Battles;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// Serializes tests that install a process-wide <c>Game.Current</c>, so no other test reads the stand-in game.
/// </summary>
[CollectionDefinition(nameof(GameCurrentCollection), DisableParallelization = true)]
public class GameCurrentCollection
{
}

/// <summary>Covers the shared scene seed and the campaign RNG swap the siege preparation handler wraps around
/// vanilla scene setup.</summary>
[Collection(nameof(GameCurrentCollection))]
public class CoopSiegeMissionPreparationHandlerTests
{
    /// <summary>Every client builds its handler from the same map-event id, so each must derive the same
    /// process-independent seed, otherwise clients pre-destroy different wall pieces.</summary>
    [Fact]
    public void SceneSeed_IsStableHashOfMapEventId_AcrossHandlerInstances()
    {
        const string mapEventId = "map-event-17";

        var first = new CoopSiegeMissionPreparationHandler(false, new[] { 1f, 1f }, false, mapEventId);
        var second = new CoopSiegeMissionPreparationHandler(true, new[] { 0.5f, 0.25f }, true, mapEventId);

        Assert.Equal(StableHash.FromString(mapEventId), first.SceneSeed);
        Assert.Equal(first.SceneSeed, second.SceneSeed);
    }

    /// <summary>A failed scene setup must still hand the campaign its own RNG back, otherwise every client keeps
    /// drawing campaign randomness from the shared siege seed.</summary>
    [Fact]
    public void OnBehaviorInitialize_RestoresCampaignRandom_WhenSceneSetupThrows()
    {
        var previousGame = Game.Current;
#pragma warning disable SYSLIB0050 // Identity-only stand-in; no native Game constructor is invoked.
        var game = (Game)FormatterServices.GetUninitializedObject(typeof(Game));
#pragma warning restore SYSLIB0050
        var campaignRandom = new MBFastRandom(7u);
        game.RandomGenerator = campaignRandom;
        Game.Current = game;
        try
        {
            var handler = new CoopSiegeMissionPreparationHandler(false, new[] { 1f, 1f }, false, "map-event-17");

            // No Mission.Current is installed, so vanilla SetUpScene throws reading the mission scene.
            Assert.ThrowsAny<Exception>(() => handler.OnBehaviorInitialize());

            Assert.Same(campaignRandom, game.RandomGenerator);
        }
        finally
        {
            Game.Current = previousGame;
        }
    }
}
