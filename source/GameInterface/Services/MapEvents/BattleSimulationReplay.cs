using Common.Messaging;
using GameInterface.Services.MapEvents.Messages.Start;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// [Client] Drives the auto-resolve playback. The client's own simulation engine is disabled, so this
/// paces the battle off <c>BattleSimulation._numTicks</c> (exactly like vanilla: one round per in-game
/// second, ×6 while fast-forwarding, all-at-once on skip) and asks the server to resolve each round on
/// demand. The server's per-round casualties and <c>BattleState</c> sync back as it goes, so the map
/// event advances in lockstep with the scoreboard instead of completing instantly. Incoming rounds are
/// replayed onto the scoreboard observer here, on the main-thread tick.
/// </summary>
internal static class BattleSimulationReplay
{
    /// <summary>A resolved <c>TroopNumberChanged</c> call ready to push to the scoreboard observer.</summary>
    internal readonly struct ResolvedChange
    {
        public readonly BattleSideEnum Side;
        public readonly PartyBase Party;
        public readonly CharacterObject Character;
        public readonly int Number;
        public readonly int NumberKilled;
        public readonly int NumberWounded;
        public readonly int NumberRouted;
        public readonly int KillCount;
        public readonly int NumberReadyToUpgrade;

        public ResolvedChange(BattleSideEnum side, PartyBase party, CharacterObject character,
            int number, int numberKilled, int numberWounded, int numberRouted, int killCount, int numberReadyToUpgrade)
        {
            Side = side;
            Party = party;
            Character = character;
            Number = number;
            NumberKilled = numberKilled;
            NumberWounded = numberWounded;
            NumberRouted = numberRouted;
            KillCount = killCount;
            NumberReadyToUpgrade = numberReadyToUpgrade;
        }
    }

    // BattleSimulation._simulationState is a private nested enum; read it as an int.
    // Order: Play = 0, FastForward = 1, Skip = 2, Pause = 3.
    private const int StateFastForward = 1;
    private const int StateSkip = 2;
    private const int StatePause = 3;

    private static readonly FieldInfo SimulationStateField = AccessTools.Field(typeof(BattleSimulation), "_simulationState");

    // Sentinel round count meaning "resolve the rest of the battle now" (used for Skip).
    private const int AllRemainingRounds = int.MaxValue;

    private static readonly Queue<ResolvedChange[]> arrivedRounds = new();
    private static string mapEventId;
    private static bool finishRequested;
    private static bool skipRequested;

    /// <summary>Begin a fresh playback for the given map event.</summary>
    public static void Begin(string id)
    {
        arrivedRounds.Clear();
        mapEventId = id;
        finishRequested = false;
        skipRequested = false;
    }

    /// <summary>Queue a round streamed from the server (applied on the next tick).</summary>
    public static void EnqueueRound(ResolvedChange[] round)
    {
        arrivedRounds.Enqueue(round);
    }

    /// <summary>The server reported the simulation is complete; finish once playback drains.</summary>
    public static void RequestFinish()
    {
        finishRequested = true;
    }

    /// <summary>
    /// Advances playback for one frame: replays any rounds the server has streamed, then — paced by the
    /// simulation's own <c>_numTicks</c> — asks the server to resolve the next round(s).
    /// </summary>
    public static void Tick(BattleSimulation simulation, float dt)
    {
        if (simulation == null || mapEventId == null || simulation.IsSimulationFinished)
            return;

        // Replay everything the server has sent so far onto the scoreboard.
        while (arrivedRounds.Count > 0)
            ApplyRound(simulation, arrivedRounds.Dequeue());

        // The queue is fully drained above, so reaching here with finishRequested means the server is
        // done and every streamed round has been shown: finish playback.
        if (finishRequested)
        {
            simulation.IsSimulationFinished = true;
            simulation.BattleObserver?.BattleResultsReady();
            mapEventId = null;
            return;
        }

        int state = GetSimulationState(simulation);
        switch (state)
        {
            case StateSkip:
                // Resolve the remainder in one request; further frames just drain the tail.
                if (!skipRequested)
                {
                    skipRequested = true;
                    RequestAdvance(simulation, AllRemainingRounds);
                }
                return;
            case StatePause:
                return;
            case StateFastForward:
                dt *= 6f;
                break;
        }

        // Same cadence as BattleSimulation.Tick: one round per accumulated in-game second.
        simulation._numTicks += dt;
        while (simulation._numTicks >= 1f)
        {
            simulation._numTicks -= 1f;
            RequestAdvance(simulation, 1);
        }
    }

    private static void RequestAdvance(BattleSimulation simulation, int rounds)
    {
        MessageBroker.Instance.Publish(simulation, new RequestAdvanceBattleSimulation(mapEventId, rounds));
    }

    private static void ApplyRound(BattleSimulation simulation, ResolvedChange[] round)
    {
        var observer = simulation.BattleObserver;
        if (observer == null)
            return;

        foreach (var change in round)
        {
            observer.TroopNumberChanged(change.Side, change.Party, change.Character,
                change.Number, change.NumberKilled, change.NumberWounded, change.NumberRouted, change.KillCount, change.NumberReadyToUpgrade);
        }
    }

    private static int GetSimulationState(BattleSimulation simulation)
    {
        try
        {
            return Convert.ToInt32(SimulationStateField.GetValue(simulation));
        }
        catch
        {
            return 0; // default to Play
        }
    }
}
