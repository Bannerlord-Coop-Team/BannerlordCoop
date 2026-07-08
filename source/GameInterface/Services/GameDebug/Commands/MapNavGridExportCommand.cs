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

        // Position grid: which face covers each cell center (-1 = off-mesh).
        int width = (int)Math.Ceiling((max.x - min.x) / cellSize) + 1;
        int height = (int)Math.Ceiling((max.y - min.y) / cellSize) + 1;
        var grid = new int[width * height];
        for (int gy = 0; gy < height; gy++)
        {
            for (int gx = 0; gx < width; gx++)
            {
                var pos = new CampaignVec2(new Vec2(min.x + gx * cellSize, min.y + gy * cellSize), true);
                PathFaceRecord face = mapScene.GetFaceIndex(in pos);
                grid[gy * width + gx] =
                    face.FaceIndex >= 0 && faceOrdinalByIndex.TryGetValue(face.FaceIndex, out int ordinal)
                        ? ordinal
                        : -1;
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
                        $"{width}x{height} cells @ {cellSize:0.##}u. Copy to the headless server's CoopMapData folder.";
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
