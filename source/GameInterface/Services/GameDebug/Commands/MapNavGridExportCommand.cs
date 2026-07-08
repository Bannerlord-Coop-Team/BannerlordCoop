using Common.Logging;
using HarmonyLib;
using SandBox;
using SandBox.View.Map;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.GameDebug.Commands;

/// <summary>
/// Exports the campaign map's navigation mesh as a sampled grid, for the HEADLESS server.
///
/// The headless server has no native scene, so its terrain/navmesh queries are stubs — party
/// pathfinding there ignores water and mountains. This export runs in the REAL game (which has
/// the native scene), enumerates every navigation face (id/group/island/terrain/center) and
/// samples position→face over a regular grid, so the server can answer the same queries from
/// data. Because it samples whatever scene is loaded, it works for custom maps too.
///
/// Console: <c>coop.debug.map.export_navgrid [cellSize]</c> (default 1.0 map units).
/// Automation: set the <c>COOP_EXPORT_NAVGRID</c> environment variable and the export runs once
/// automatically when the campaign map opens.
///
/// Output: Documents\Mount and Blade II Bannerlord\CoopMapData\&lt;scene&gt;.navgrid — copy it to
/// the headless server's "CoopMapData" folder (next to its "Game Saves").
/// </summary>
public class MapNavGridExportCommand
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapNavGridExportCommand>();

    /// <summary>Format magic/version — keep in sync with the ServerHeadless reader.</summary>
    private const uint FormatMagic = 0x434E4156; // "VANC" little-endian for "CNAV"
    private const int FormatVersion = 1;

    [CommandLineArgumentFunction("export_navgrid", "coop.debug.map")]
    public static string ExportNavGrid(List<string> args)
    {
        float cellSize = 1.0f;
        if (args.Count > 0 && float.TryParse(args[0], out float parsed) && parsed > 0.05f)
        {
            cellSize = parsed;
        }

        try
        {
            return Export(cellSize);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "NavGrid export failed");
            return $"NavGrid export failed: {ex.Message}";
        }
    }

    internal static string Export(float cellSize)
    {
        if (Campaign.Current?.MapSceneWrapper is not MapScene mapScene || mapScene.Scene == null)
        {
            return "No native campaign map scene loaded (this command needs the real game).";
        }

        mapScene.GetMapBorders(out Vec2 min, out Vec2 max, out float maxHeight);

        // Face table: every navigation face with its identity and terrain.
        int faceCount = mapScene.GetNumberOfNavigationMeshFaces();
        var faceOrdinalByIndex = new Dictionary<int, int>(faceCount);
        var faces = new List<(int index, int group, int island, int terrain, Vec2 center, float z)>(faceCount);
        for (int i = 0; i < faceCount; i++)
        {
            PathFaceRecord face = mapScene.GetFaceAtIndex(i);
            var terrain = mapScene.GetFaceTerrainType(face);
            Vec2 center = mapScene.GetNavigationMeshCenterPosition(i);
            float z = mapScene.GetFaceVertexZ(face);
            faceOrdinalByIndex[face.FaceIndex] = faces.Count;
            faces.Add((face.FaceIndex, face.FaceGroupIndex, face.FaceIslandIndex, (int)terrain, center, z));
        }

        // Position grid: which face covers each cell (-1 = off-mesh). Sampled with the STRICT
        // containment query GetNavigationMeshForPosition (null pointer when no face contains the
        // position) — the face-index queries snap to the NEAREST face, which painted settlement
        // footprints (navmesh holes) as walkable.
        //
        // Cells whose center lands on path-INVALID terrain (river, mountain, canyon — the
        // PartyNavigationModel's list) are SUPERSAMPLED: probe offset points and from above, and
        // prefer any path-valid face the cell touches. Fords and bridges are thin slivers
        // (1-7 cells at 1u before this) and elevated bridge decks lose the z=0 probe to the river
        // face underneath — center-only sampling left river crossings unusable and the AI could
        // not path over them.
        var navigationModel = Campaign.Current.Models.PartyNavigationModel;
        bool IsPathValid(int ordinal) => navigationModel.IsTerrainTypeValidForNavigationType(
            (TerrainType)faces[ordinal].terrain, MobileParty.NavigationType.Default);

        int width = (int)Math.Ceiling((max.x - min.x) / cellSize) + 1;
        int height = (int)Math.Ceiling((max.y - min.y) / cellSize) + 1;
        var grid = new int[width * height];
        long offMesh = 0;
        long supersampled = 0;
        float heightLimit = maxHeight + 200f;
        float highZ = maxHeight + 50f;

        int ProbeOrdinal(float x, float y, float z)
        {
            var p3 = new Vec3(x, y, z);
            UIntPtr facePtr = mapScene.Scene.GetNavigationMeshForPosition(in p3, out _, heightLimit, false);
            if (facePtr == UIntPtr.Zero) return -1;
            PathFaceRecord record = mapScene.Scene.GetPathFaceRecordFromNavMeshFacePointer(facePtr);
            return record.FaceIndex >= 0 && faceOrdinalByIndex.TryGetValue(record.FaceIndex, out int ordinal)
                ? ordinal
                : -1;
        }

        float off = 0.35f * cellSize;
        var offsets = new[]
        {
            new Vec2(-off, -off), new Vec2(off, -off), new Vec2(-off, off), new Vec2(off, off),
        };

        for (int gy = 0; gy < height; gy++)
        {
            for (int gx = 0; gx < width; gx++)
            {
                float x = min.x + gx * cellSize;
                float y = min.y + gy * cellSize;

                int cell = ProbeOrdinal(x, y, 0f);
                if (cell < 0 || !IsPathValid(cell))
                {
                    // Look for a walkable face this cell touches: from above at the center (an
                    // elevated bridge deck shadowed by the water face below), then the corners at
                    // both heights (thin ford slivers off the cell center).
                    supersampled++;
                    int best = cell;
                    var probes = new (float px, float py, float pz)[]
                    {
                        (x, y, highZ),
                        (x + offsets[0].x, y + offsets[0].y, 0f), (x + offsets[1].x, y + offsets[1].y, 0f),
                        (x + offsets[2].x, y + offsets[2].y, 0f), (x + offsets[3].x, y + offsets[3].y, 0f),
                        (x + offsets[0].x, y + offsets[0].y, highZ), (x + offsets[1].x, y + offsets[1].y, highZ),
                        (x + offsets[2].x, y + offsets[2].y, highZ), (x + offsets[3].x, y + offsets[3].y, highZ),
                    };
                    foreach (var (px, py, pz) in probes)
                    {
                        int candidate = ProbeOrdinal(px, py, pz);
                        if (candidate < 0) continue;
                        if (best < 0) best = candidate;
                        if (IsPathValid(candidate)) { best = candidate; break; }
                    }
                    cell = best;
                }

                if (cell < 0) offMesh++;
                grid[gy * width + gx] = cell;
            }
        }

        string sceneName = mapScene.Scene.GetName();
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Mount and Blade II Bannerlord", "CoopMapData");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, sceneName + ".navgrid");

        using (var stream = new GZipStream(File.Create(file), CompressionLevel.Optimal))
        using (var writer = new System.IO.BinaryWriter(stream))
        {
            writer.Write(FormatMagic);
            writer.Write(FormatVersion);
            writer.Write(sceneName);
            writer.Write(mapScene.GetSceneXmlCrc());
            writer.Write(mapScene.GetSceneNavigationMeshCrc());
            writer.Write(min.x); writer.Write(min.y);
            writer.Write(max.x); writer.Write(max.y);
            writer.Write(maxHeight);
            writer.Write(cellSize);
            writer.Write(width); writer.Write(height);

            writer.Write(faces.Count);
            foreach (var f in faces)
            {
                writer.Write(f.index);
                writer.Write(f.group);
                writer.Write(f.island);
                writer.Write(f.terrain);
                writer.Write(f.center.x); writer.Write(f.center.y);
                writer.Write(f.z);
            }

            foreach (int cell in grid)
            {
                writer.Write(cell);
            }
        }

        string report = $"NavGrid exported: '{file}' — scene '{sceneName}', {faces.Count:N0} faces, " +
                        $"{width}x{height} cells @ {cellSize:0.##}u ({offMesh:N0} off-mesh, " +
                        $"{100.0 * offMesh / (width * (long)height):0.0}%; {supersampled:N0} supersampled). " +
                        "Copy to the headless server's CoopMapData folder.";
        Logger.Information(report);
        return report;
    }
}

/// <summary>
/// One-shot automated export: with the <c>COOP_EXPORT_NAVGRID</c> environment variable set, the
/// export runs as soon as the campaign map screen opens (headless hosts have no map screen, so
/// this can only fire in the real game).
/// </summary>
[HarmonyPatch(typeof(MapScreen), "OnInitialize")]
internal class MapNavGridAutoExportPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapNavGridExportCommand>();
    private static bool _exported;

    static void Postfix()
    {
        if (_exported || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COOP_EXPORT_NAVGRID")))
            return;

        _exported = true;
        try
        {
            Logger.Information("COOP_EXPORT_NAVGRID set — {result}", MapNavGridExportCommand.Export(1.0f));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Automated navgrid export failed");
        }
    }
}
