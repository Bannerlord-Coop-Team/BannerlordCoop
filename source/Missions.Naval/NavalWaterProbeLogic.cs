#if DEBUG
using Common.Logging;
using Missions.Battles;
using NavalDLC.Missions.Objects;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Engine.Options;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// [Game thread] DEBUG: while started, logs one <c>NAVAL_WATER_PROBE</c> JSON line per physics step with every clock this
/// client has, the water height at a fixed grid (the renderer-backed query NavalPhysics uses) and the position, yaw, heave,
/// pitch and roll of every registered hull, own or copy. Run on every client at once; the logs are diffed offline to see whether the
/// water clock is shared. Read only: it changes nothing and sends nothing.
/// </summary>
public class NavalWaterProbeLogic : MissionLogic
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalWaterProbeLogic>();

    internal const string Tag = "NAVAL_WATER_PROBE ";

    // Without a fixed step for this long the probe samples once per frame instead.
    private const float FixedTickTimeoutSeconds = 0.5f;

    private readonly IBattleSession session;
    private readonly INetworkShipRegistry registry;

    private NavalWaterProbeSettings settings;
    private string probeId;
    private Vec2[] points;
    private float[] heights;
    private Vec3[] normals;
    private float startedAt;
    private float sinceFixedTick = float.MaxValue;
    private long ticks;
    private long frames;

    public NavalWaterProbeLogic(IBattleSession session, INetworkShipRegistry registry)
    {
        this.session = session;
        this.registry = registry;
    }

    public bool IsRunning => settings != null;

    /// <summary>Starts a probe; why it could not, or null.</summary>
    internal string Start(NavalWaterProbeSettings probeSettings)
    {
        var scene = Mission.Scene;
        if (scene == null) return "The mission has no scene.";
        if (IsRunning) Stop("restarted");

        scene.GetSceneLimits(out var min, out var max);
        var center = new Vec2((min.x + max.x) / 2f, (min.y + max.y) / 2f);
        settings = probeSettings;
        probeId = Guid.NewGuid().ToString("N").Substring(0, 8);
        points = GridPoints(center, settings.Grid, settings.Spacing);
        heights = new float[points.Length];
        normals = new Vec3[points.Length];
        startedAt = Mission.CurrentTime;
        ticks = 0;
        Log(Header(probeId, session.OwnControllerId, session.InstanceId, settings, points, ProbeEnvironment(scene, min, max)));
        return null;
    }

    internal void Stop(string reason)
    {
        if (!IsRunning) return;

        Log(End(probeId, ticks, reason));
        settings = null;
    }

    internal object Status() => new
    {
        running = IsRunning,
        probe = probeId,
        grid = settings?.Grid,
        spacing = settings?.Spacing,
        seconds = settings?.Seconds,
        ticks,
        secondsLeft = IsRunning ? (float?)Math.Max(0f, settings.Seconds - (Mission.CurrentTime - startedAt)) : null,
    };

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        frames++;
        if (!IsRunning) return;

        if (Mission.CurrentTime - startedAt >= settings.Seconds)
        {
            Stop("elapsed");
            return;
        }

        sinceFixedTick += dt;
        if (sinceFixedTick > FixedTickTimeoutSeconds) Sample(dt);
    }

    public override void OnFixedMissionTick(float fixedDt)
    {
        base.OnFixedMissionTick(fixedDt);
        sinceFixedTick = 0f;
        if (IsRunning) Sample(fixedDt);
    }

    public override void OnEndMissionInternal()
    {
        Stop("mission_end");
        base.OnEndMissionInternal();
    }

    private void Sample(float dt)
    {
        var scene = Mission.Scene;
        scene.GetBulkWaterLevelAtPositions(points, ref heights, ref normals);
        ticks++;
        Log(Tick(probeId, ticks, frames, dt, Mission.CurrentTime, MBCommon.GetTotalMissionTime(), Time.ApplicationTime,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), heights, Hulls(scene)));
    }

    private IEnumerable<NavalWaterProbeHull> Hulls(Scene scene)
    {
        foreach (var ship in registry.Ships)
        {
            if (!(ship.Hull is MissionShip hull) || hull.IsRemoved || !hull.GameEntity.IsValid) continue;

            var frame = hull.GlobalFrame;
            bool isCopy = NavalForeignHulls.Contains(hull);
            yield return new NavalWaterProbeHull(NavalContactProbe.Short(ship.ShipId), isCopy ? "copy" : "own", CopyDiagnostics.ModeName(hull),
                frame.origin.x, frame.origin.y, YawDegrees(frame.rotation.f), frame.origin.z, AxisTiltDegrees(frame.rotation.f.z),
                AxisTiltDegrees(frame.rotation.s.z), scene.GetWaterLevelAtPosition(frame.origin.AsVec2, true, false));
        }
    }

    // The settings and scene state that decide the water and the physics step on this client.
    private static Dictionary<string, object> ProbeEnvironment(Scene scene, Vec3 min, Vec3 max)
    {
        var wind = scene.GetGlobalWindVelocity();
        return new Dictionary<string, object>
        {
            ["waterQuality"] = NativeOptions.GetConfig(NativeOptions.NativeOptionsType.WaterQuality),
            ["physicsTickRate"] = NativeOptions.GetConfig(NativeOptions.NativeOptionsType.PhysicsTickRate),
            ["waterStrength"] = scene.GetWaterStrength(),
            ["wind"] = new[] { wind.x, wind.y },
            ["north"] = scene.GetNorthRotation(),
            ["timeSpeed"] = scene.TimeSpeed,
            ["sceneMin"] = new[] { min.x, min.y },
            ["sceneMax"] = new[] { max.x, max.y },
        };
    }

    private static void Log(string line) => Logger.Information("{ProbeLine:l}", line);

    internal static float AxisTiltDegrees(float axisZ) =>
        (float)(Math.Asin(Math.Max(-1f, Math.Min(1f, axisZ))) * 180.0 / Math.PI);

    /// <summary>The heading of <paramref name="forward"/> on the water plane in degrees, counter-clockwise from +x, in (-180, 180].</summary>
    internal static float YawDegrees(Vec3 forward) => (float)(Math.Atan2(forward.y, forward.x) * 180.0 / Math.PI);

    /// <summary>A grid × grid square of points <paramref name="spacing"/> metres apart, centred on <paramref name="center"/>, row by row.</summary>
    internal static Vec2[] GridPoints(Vec2 center, int grid, float spacing)
    {
        var result = new Vec2[grid * grid];
        float half = (grid - 1) * spacing / 2f;
        for (int row = 0; row < grid; row++)
        {
            for (int column = 0; column < grid; column++)
                result[(row * grid) + column] = new Vec2(center.x - half + (column * spacing), center.y - half + (row * spacing));
        }

        return result;
    }

    internal static string Header(string probeId, string controllerId, string instanceId, NavalWaterProbeSettings probeSettings,
        Vec2[] gridPoints, IDictionary<string, object> environment) => Tag + JsonConvert.SerializeObject(new Dictionary<string, object>
    {
        ["t"] = "header",
        ["probe"] = probeId,
        ["controller"] = controllerId,
        ["instance"] = instanceId,
        ["grid"] = probeSettings.Grid,
        ["spacing"] = probeSettings.Spacing,
        ["seconds"] = probeSettings.Seconds,
        ["points"] = gridPoints.Select(point => new[] { Round(point.x, 2), Round(point.y, 2) }).ToArray(),
        ["env"] = environment,
    });

    internal static string Tick(string probeId, long tick, long frame, float dt, float missionTime, float totalMissionTime,
        float applicationTime, long utcMilliseconds, IReadOnlyList<float> gridHeights, IEnumerable<NavalWaterProbeHull> hulls) =>
        Tag + JsonConvert.SerializeObject(new Dictionary<string, object>
        {
            ["t"] = "tick",
            ["probe"] = probeId,
            ["tick"] = tick,
            ["frame"] = frame,
            ["dt"] = Round(dt, 5),
            ["mt"] = Round(missionTime, 4),
            ["total"] = Round(totalMissionTime, 4),
            ["app"] = Round(applicationTime, 4),
            ["utc"] = utcMilliseconds,
            ["h"] = gridHeights.Select(height => Round(height, 4)).ToArray(),
            ["hulls"] = hulls.Select(hull => new Dictionary<string, object>
            {
                ["id"] = hull.ShipId,
                ["role"] = hull.Role,
                ["mode"] = hull.Mode,
                ["x"] = Round(hull.X, 3),
                ["y"] = Round(hull.Y, 3),
                ["yaw"] = Round(hull.Yaw, 3),
                ["z"] = Round(hull.Heave, 4),
                ["pitch"] = Round(hull.Pitch, 3),
                ["roll"] = Round(hull.Roll, 3),
                ["w"] = Round(hull.Water, 4),
            }).ToArray(),
        });

    internal static string End(string probeId, long tickCount, string reason) =>
        Tag + JsonConvert.SerializeObject(new Dictionary<string, object>
        {
            ["t"] = "end",
            ["probe"] = probeId,
            ["ticks"] = tickCount,
            ["reason"] = reason,
        });

    private static double Round(float value, int digits) =>
        float.IsNaN(value) || float.IsInfinity(value) ? 0d : Math.Round(value, digits);
}
#endif
