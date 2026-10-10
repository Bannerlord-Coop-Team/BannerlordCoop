using Coop.Naval.Storms.Data;
using GameInterface.Services;
using HarmonyLib;
using NavalDLC;
using NavalDLC.Map;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Naval.Storms.Interfaces;

public interface IStormInterface : IGameAbstraction
{
    IReadOnlyList<Storm> SpawnedStorms { get; }
    bool IsSpawned(Storm storm);
    StormStateData GetState(Storm storm);
    void ApplyState(Storm storm, StormStateData state);
    void AddSpawnedStorm(Storm storm);
    void RemoveSpawnedStorm(Storm storm);
}

public class StormInterface : IStormInterface
{
    private const int PreviousDataFieldCount = 3;

    // StormType is readonly, which the publicizer cant make assignable
    private static readonly AccessTools.FieldRef<Storm, Storm.StormTypes> StormTypeRef =
        AccessTools.FieldRefAccess<Storm, Storm.StormTypes>(nameof(Storm.StormType));

    private static StormManager StormManager => NavalDLCManager.Instance?.StormManager;

    public IReadOnlyList<Storm> SpawnedStorms => (IReadOnlyList<Storm>)StormManager?._spawnedStorms ?? new List<Storm>();

    public bool IsSpawned(Storm storm) => StormManager?._spawnedStorms.Contains(storm) == true;

    public StormStateData GetState(Storm storm)
    {
        var previousData = storm._previousPositionsAndRadius ?? Array.Empty<Storm.PreviousData>();
        var flattenedPreviousData = new float[previousData.Length * PreviousDataFieldCount];
        for (int i = 0; i < previousData.Length; i++)
        {
            flattenedPreviousData[i * PreviousDataFieldCount] = previousData[i].Position.X;
            flattenedPreviousData[(i * PreviousDataFieldCount) + 1] = previousData[i].Position.Y;
            flattenedPreviousData[(i * PreviousDataFieldCount) + 2] = previousData[i].EffectRadius;
        }

        return new StormStateData(
            (int)storm.StormType,
            storm._currentPosition.X,
            storm._currentPosition.Y,
            storm._intensity,
            storm._speed,
            storm._developingStateFinishCampaignTime.NumTicks,
            storm._finalizingStateStartCampaignTime.NumTicks,
            storm._desiredMoveDirection.X,
            storm._desiredMoveDirection.Y,
            storm._currentMoveDirection.X,
            storm._currentMoveDirection.Y,
            flattenedPreviousData,
            storm._nextUpdatePreviousDataArrayIndex,
            storm._nextUpdateTime.NumTicks);
    }

    public void ApplyState(Storm storm, StormStateData state)
    {
        StormTypeRef(storm) = (Storm.StormTypes)state.StormType;

        // Fields are written directly so the setters dont deactivate or clamp a value the server already settled
        storm._currentPosition = new Vec2(state.PositionX, state.PositionY);
        storm._intensity = state.Intensity;
        storm._speed = state.Speed;
        storm._developingStateFinishCampaignTime = new CampaignTime(state.DevelopingStateFinishTicks);
        storm._finalizingStateStartCampaignTime = new CampaignTime(state.FinalizingStateStartTicks);
        storm._desiredMoveDirection = new Vec2(state.DesiredMoveDirectionX, state.DesiredMoveDirectionY);
        storm._currentMoveDirection = new Vec2(state.CurrentMoveDirectionX, state.CurrentMoveDirectionY);
        storm._previousPositionsAndRadius = ToPreviousData(state.PreviousPositionsAndRadius);
        storm._nextUpdatePreviousDataArrayIndex = state.NextUpdatePreviousDataArrayIndex;
        storm._nextUpdateTime = new CampaignTime(state.NextUpdateTicks);

        storm.SetVisualDirty();
    }

    public void AddSpawnedStorm(Storm storm)
    {
        if (StormManager == null || IsSpawned(storm)) return;

        StormManager._spawnedStorms.Add(storm);

        // StormVisualManager creates the visual from this event, same as a vanilla spawn
        NavalDLCEvents.Instance.OnStormCreated(storm);
    }

    public void RemoveSpawnedStorm(Storm storm)
    {
        if (StormManager == null) return;

        storm.SetVisualDirty();
        StormManager._spawnedStorms.Remove(storm);
    }

    private static Storm.PreviousData[] ToPreviousData(float[] flattened)
    {
        flattened ??= Array.Empty<float>();

        var count = flattened.Length / PreviousDataFieldCount;
        var previousData = new Storm.PreviousData[count];
        for (int i = 0; i < count; i++)
        {
            previousData[i] = new Storm.PreviousData(
                new Vec2(flattened[i * PreviousDataFieldCount], flattened[(i * PreviousDataFieldCount) + 1]),
                flattened[(i * PreviousDataFieldCount) + 2]);
        }

        return previousData;
    }
}
