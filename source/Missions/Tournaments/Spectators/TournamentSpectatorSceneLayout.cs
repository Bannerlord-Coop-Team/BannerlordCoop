using System;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace Missions.Tournaments.Spectators;

public readonly struct TournamentSpectatorBarrierData
{
    public readonly string PrefabName;
    public readonly Vec3 Position;
    public readonly float Rotation;
    public readonly Vec3 Scale;

    public TournamentSpectatorBarrierData(string prefabName, Vec3 position, float rotation, Vec3 scale)
    {
        PrefabName = prefabName;
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }
}

public readonly struct TournamentSpectatorSpawnData
{
    public readonly int SpawnId;
    public readonly Vec3 Position;
    public readonly float Rotation;

    public TournamentSpectatorSpawnData(int spawnId, Vec3 position, float rotation)
    {
        SpawnId = spawnId;
        Position = position;
        Rotation = rotation;
    }

    public string Name => $"Spawn {SpawnId}";
}

public sealed class TournamentSpectatorSceneLayout
{
    public IReadOnlyList<TournamentSpectatorBarrierData> Barriers { get; }
    public IReadOnlyList<TournamentSpectatorSpawnData> Spawns { get; }

    public TournamentSpectatorSceneLayout(
        TournamentSpectatorBarrierData[] barriers,
        TournamentSpectatorSpawnData[] spawns)
    {
        Barriers = Array.AsReadOnly(barriers ?? Array.Empty<TournamentSpectatorBarrierData>());
        Spawns = Array.AsReadOnly(spawns ?? Array.Empty<TournamentSpectatorSpawnData>());
    }
}

public static class TournamentSpectatorSceneLayouts
{
    public const string EmpireArenaScene = "arena_empire_a";
    public const string AseraiArenaScene = "arena_aserai_a";
    public const string BattaniaArenaScene = "arena_battania_a";
    public const string KhuzaitArenaScene = "arena_khuzait_a";
    public const string SturgiaArenaScene = "arena_sturgia_a";
    public const string VlandiaArenaScene = "arena_vlandia_a";

    public static bool TryGet(string sceneName, out TournamentSpectatorSceneLayout layout)
    {
        if (sceneName == EmpireArenaScene)
        {
            layout = TournamentSpectatorEmpireArenaLayout.Layout;
            return true;
        }

        if (sceneName == AseraiArenaScene)
        {
            layout = TournamentSpectatorAseraiArenaLayout.Layout;
            return true;
        }

        if (sceneName == BattaniaArenaScene)
        {
            layout = TournamentSpectatorBattaniaArenaLayout.Layout;
            return true;
        }

        if (sceneName == KhuzaitArenaScene)
        {
            layout = TournamentSpectatorKhuzaitArenaLayout.Layout;
            return true;
        }

        if (sceneName == SturgiaArenaScene)
        {
            layout = TournamentSpectatorSturgiaArenaLayout.Layout;
            return true;
        }

        if (sceneName == VlandiaArenaScene)
        {
            layout = TournamentSpectatorVlandiaArenaLayout.Layout;
            return true;
        }

        layout = null;
        return false;
    }
}
