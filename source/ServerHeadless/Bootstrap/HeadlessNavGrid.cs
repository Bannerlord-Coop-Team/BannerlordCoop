using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ServerHeadless.Bootstrap
{
    /// <summary>
    /// The campaign map's navigation data, loaded from a grid exported by the REAL game
    /// (GameInterface's <c>coop.debug.map.export_navgrid</c> — see MapNavGridExportCommand for the
    /// format). Gives the headless server real terrain: position→face lookups, per-face terrain
    /// types and islands, and A* pathfinding that respects water and mountains — the native scene
    /// this replaces is what normally answers all of these.
    ///
    /// The export samples whatever scene the exporting game had loaded, so custom maps work: the
    /// operator runs the export once on their map and drops the file in the server's CoopMapData
    /// folder (next to "Game Saves"). Without a file the server falls back to the old
    /// terrain-blind stubs, loudly.
    /// </summary>
    internal sealed class HeadlessNavGrid
    {
        private const uint FormatMagic = 0x434E4156; // matches MapNavGridExportCommand
        private const int FormatVersion = 1;

        /// <summary>Loaded grid, or null when no export file exists (terrain-blind fallback).</summary>
        public static HeadlessNavGrid Instance { get; private set; }

        public struct Face
        {
            public int Index;
            public int Group;
            public int Island;
            public int Terrain;
            public Vec2 Center;
            public float Z;
        }

        public string SceneName;
        public uint SceneXmlCrc;
        public uint NavMeshCrc;
        public Vec2 Min;
        public Vec2 Max;
        public float MaxHeight;
        public float CellSize;
        public int Width;
        public int Height;
        public Face[] Faces;

        /// <summary>Cell → ordinal into <see cref="Faces"/>, -1 = off-mesh.</summary>
        private int[] _grid;
        private Dictionary<int, int> _ordinalByFaceIndex;

        // Per-thread A* scratch: the Coop mod's parallel campaign tick
        // (ParallelRobustnessPatches.ParallelTickMovingParties) pathfinds from many worker
        // threads at once. Shared buffers corrupt under that race, and serializing behind a
        // lock stalls the whole tick once live-war AI load saturates pathfinding — so every
        // thread gets its own buffers (~5MB each at Main_map size).
        private sealed class SearchScratch
        {
            public float[] GScore;
            public int[] CameFrom;
            public int[] VisitStamp;
            public int Stamp;
            public readonly List<int> OpenHeap = new List<int>();
            public readonly List<float> HeapScores = new List<float>();
        }

        [ThreadStatic] private static SearchScratch _threadScratch;

        private SearchScratch GetScratch()
        {
            var scratch = _threadScratch;
            int cells = Width * Height;
            if (scratch == null || scratch.GScore.Length != cells)
            {
                scratch = new SearchScratch
                {
                    GScore = new float[cells],
                    CameFrom = new int[cells],
                    VisitStamp = new int[cells],
                };
                _threadScratch = scratch;
            }
            return scratch;
        }

        /// <summary>Max A* expansions before giving up (a full cross-map corridor is far below this).</summary>
        private const int MaxExpansions = 120_000;

        /// <summary>
        /// Connected-component ids per cell for the two navigation layers: LAND (water excluded)
        /// and ALL (everything on-mesh, i.e. naval-capable movement). A* and distance estimates
        /// pre-check these so unreachable queries fail instantly — without them, every
        /// party targeting across a water gap exhausts the whole grid per query, which stalls
        /// campaign load for minutes.
        /// </summary>
        private int[] _landRegion;
        private int[] _allRegion;

        private void ComputeRegions()
        {
            _landRegion = FloodFillRegions(waterAllowed: false);
            _allRegion = FloodFillRegions(waterAllowed: true);
        }

        private int[] FloodFillRegions(bool waterAllowed)
        {
            int cells = Width * Height;
            var regions = new int[cells];
            for (int i = 0; i < cells; i++) regions[i] = -1;

            var queue = new Queue<int>();
            int nextRegion = 0;
            for (int seed = 0; seed < cells; seed++)
            {
                if (regions[seed] != -1) continue;
                int ordinal = _grid[seed];
                if (ordinal < 0) continue;
                if (!waterAllowed && IsWater((TerrainType)Faces[ordinal].Terrain)) continue;

                int region = nextRegion++;
                regions[seed] = region;
                queue.Enqueue(seed);
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    int cgx = current % Width, cgy = current / Width;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int ngx = cgx + dx, ngy = cgy + dy;
                            if (ngx < 0 || ngy < 0 || ngx >= Width || ngy >= Height) continue;
                            int neighbor = ngy * Width + ngx;
                            if (regions[neighbor] != -1) continue;
                            int no = _grid[neighbor];
                            if (no < 0) continue;
                            if (!waterAllowed && IsWater((TerrainType)Faces[no].Terrain)) continue;
                            regions[neighbor] = region;
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }
            return regions;
        }

        /// <summary>Do the excluded terrains rule out water travel (i.e. is this a land-only query)?</summary>
        private static bool ExcludesWater(int[] excludedTerrains)
            => IsExcluded((int)TerrainType.Water, excludedTerrains);

        // Query diagnostics: counts + cumulative time, reported every few seconds when active.
        // A single method dominating by millions of calls = a vanilla caller retry-looping on our
        // answers; high time with modest calls = a genuinely slow query.
        private static readonly long[] _queryCalls = new long[4];
        private static readonly long[] _queryTicks = new long[4];
        private static readonly string[] _queryNames = { "FindPath", "EstimateDistance", "NearestAllowed", "LineClear" };
        private static long _lastReportTicks = DateTime.UtcNow.Ticks;

        private static void Track(int queryIndex, long elapsedTicks)
        {
            _queryCalls[queryIndex]++;
            _queryTicks[queryIndex] += elapsedTicks;

            long now = DateTime.UtcNow.Ticks;
            if (now - _lastReportTicks > TimeSpan.TicksPerSecond * 5)
            {
                _lastReportTicks = now;
                var parts = new List<string>();
                for (int i = 0; i < _queryCalls.Length; i++)
                {
                    if (_queryCalls[i] > 0)
                        parts.Add($"{_queryNames[i]}: {_queryCalls[i]:N0} calls, {TimeSpan.FromTicks(_queryTicks[i]).TotalSeconds:0.0}s");
                }
                Console.WriteLine("[ServerHeadless] NavGrid queries — " + string.Join("; ", parts));
            }
        }

        /// <summary>Instant reachability check via the precomputed connectivity layers.</summary>
        private bool AreConnected(int startCell, int endCell, int[] excludedTerrains)
        {
            var regions = ExcludesWater(excludedTerrains) ? _landRegion : _allRegion;
            int a = regions[startCell];
            int b = regions[endCell];
            return a != -1 && a == b;
        }

        /// <summary>
        /// Loads the newest .navgrid from <paramref name="userRoot"/>\CoopMapData, falling back to
        /// CoopMapData next to the executable (docker images bake it there, because a mounted
        /// /data volume shadows anything baked into the volume path).
        /// </summary>
        public static void TryLoad(string userRoot)
        {
            string file = null;
            string primaryDir = Path.Combine(userRoot, "CoopMapData");
            foreach (string dir in new[] { primaryDir, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CoopMapData") })
            {
                if (!Directory.Exists(dir)) continue;
                file = Directory.GetFiles(dir, "*.navgrid").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
                if (file != null) break;
            }

            if (file == null)
            {
                Console.WriteLine("[ServerHeadless] No nav grid found — terrain-blind pathfinding. " +
                    $"Export one from the real game (coop.debug.map.export_navgrid) into {primaryDir}");
                return;
            }

            try
            {
                var grid = Load(file);
                Instance = grid;
                Console.WriteLine($"[ServerHeadless] Nav grid loaded: '{Path.GetFileName(file)}' — scene '{grid.SceneName}', " +
                    $"{grid.Faces.Length:N0} faces, {grid.Width}x{grid.Height} cells @ {grid.CellSize:0.##}u.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ServerHeadless] Failed to load nav grid '{file}': {ex.Message} — terrain-blind fallback.");
            }
        }

        private static HeadlessNavGrid Load(string file)
        {
            using var stream = new GZipStream(File.OpenRead(file), CompressionMode.Decompress);
            using var reader = new System.IO.BinaryReader(stream);

            if (reader.ReadUInt32() != FormatMagic) throw new InvalidDataException("bad magic");
            int version = reader.ReadInt32();
            if (version != FormatVersion) throw new InvalidDataException($"unsupported version {version}");

            var grid = new HeadlessNavGrid
            {
                SceneName = reader.ReadString(),
                SceneXmlCrc = reader.ReadUInt32(),
                NavMeshCrc = reader.ReadUInt32(),
            };
            grid.Min = new Vec2(reader.ReadSingle(), reader.ReadSingle());
            grid.Max = new Vec2(reader.ReadSingle(), reader.ReadSingle());
            grid.MaxHeight = reader.ReadSingle();
            grid.CellSize = reader.ReadSingle();
            grid.Width = reader.ReadInt32();
            grid.Height = reader.ReadInt32();

            int faceCount = reader.ReadInt32();
            grid.Faces = new Face[faceCount];
            grid._ordinalByFaceIndex = new Dictionary<int, int>(faceCount);
            for (int i = 0; i < faceCount; i++)
            {
                var face = new Face
                {
                    Index = reader.ReadInt32(),
                    Group = reader.ReadInt32(),
                    Island = reader.ReadInt32(),
                    Terrain = reader.ReadInt32(),
                    Center = new Vec2(reader.ReadSingle(), reader.ReadSingle()),
                    Z = reader.ReadSingle(),
                };
                grid.Faces[i] = face;
                grid._ordinalByFaceIndex[face.Index] = i;
            }

            int cells = grid.Width * grid.Height;
            grid._grid = new int[cells];
            for (int i = 0; i < cells; i++)
            {
                grid._grid[i] = reader.ReadInt32();
            }

            grid.ComputeRegions();
            return grid;
        }

        // ---- basic lookups -------------------------------------------------------------------

        private int CellOf(Vec2 pos)
        {
            int gx = (int)((pos.x - Min.x) / CellSize + 0.5f);
            int gy = (int)((pos.y - Min.y) / CellSize + 0.5f);
            if (gx < 0 || gy < 0 || gx >= Width || gy >= Height) return -1;
            return gy * Width + gx;
        }

        private Vec2 CenterOf(int cell)
            => new Vec2(Min.x + (cell % Width) * CellSize, Min.y + (cell / Width) * CellSize);

        public int OrdinalAt(Vec2 pos)
        {
            int cell = CellOf(pos);
            return cell < 0 ? -1 : _grid[cell];
        }

        public PathFaceRecord FaceRecordAt(Vec2 pos)
        {
            int ordinal = OrdinalAt(pos);
            if (ordinal >= 0) return MakeRecord(Faces[ordinal]);

            // Off-mesh positions (settlement interiors sit in navmesh holes) resolve to the
            // nearest face within a small radius, like the native scene does — otherwise every
            // path targeting a settlement asserts "Path finding target is not valid".
            if (TryGetNearestAllowedPoint(pos, null, CellSize * 8f, out Vec2 snapped))
            {
                int snappedOrdinal = OrdinalAt(snapped);
                if (snappedOrdinal >= 0) return MakeRecord(Faces[snappedOrdinal]);
            }

            return PathFaceRecord.NullFaceRecord;
        }

        /// <summary>
        /// Builds the PathFaceRecord handed to campaign code. The exported per-face group ids are
        /// navmesh-internal (11 distinct on Main_map) and vanilla managed code pre-validates
        /// "same navigation group" before pathing — heterogeneous groups make the AI reject every
        /// cross-group target and hold forever. What those checks semantically want is the
        /// navigation LAYER, so groups collapse to land(0)/sea(1); islands come from the export
        /// (homogeneous on connected maps).
        /// </summary>
        public PathFaceRecord MakeRecord(in Face f)
            => new PathFaceRecord(f.Index, IsWater((TerrainType)f.Terrain) ? 1 : 0, f.Island);

        public bool TryGetFaceByIndex(int faceIndex, out Face face)
        {
            if (_ordinalByFaceIndex.TryGetValue(faceIndex, out int ordinal))
            {
                face = Faces[ordinal];
                return true;
            }
            face = default;
            return false;
        }

        public TerrainType TerrainOfFaceIndex(int faceIndex)
            => TryGetFaceByIndex(faceIndex, out var face) ? (TerrainType)face.Terrain : TerrainType.Plain;

        public TerrainType TerrainAt(Vec2 pos)
        {
            int ordinal = OrdinalAt(pos);
            return ordinal < 0 ? TerrainType.Plain : (TerrainType)Faces[ordinal].Terrain;
        }

        private static bool IsExcluded(int terrain, int[] excludedTerrains)
        {
            if (excludedTerrains == null) return false;
            for (int i = 0; i < excludedTerrains.Length; i++)
            {
                if (excludedTerrains[i] == terrain) return true;
            }
            return false;
        }

        private bool IsCellAllowed(int cell, int[] excludedTerrains)
        {
            int ordinal = _grid[cell];
            return ordinal >= 0 && !IsExcluded(Faces[ordinal].Terrain, excludedTerrains);
        }

        // Sea faces only: the navmesh contains ONLY navigable faces, so River/Lake/Mountain faces
        // being present means they are crossable (fords, bridges, passes) and belong to the LAND
        // layer. Treating rivers as water shatters the land connectivity into fragments and every
        // cross-river path request fails.
        private static bool IsWater(TerrainType terrain)
            => terrain == TerrainType.Water || terrain == TerrainType.CoastalSea || terrain == TerrainType.OpenSea;

        /// <summary>
        /// Default exclusion for position-accessibility queries that pass no exclusions: the native
        /// scene implicitly restricts those to walkable LAND faces (the navmesh also covers water
        /// for naval movement), so "no exclusions" must not mean "water is fine to stand on".
        /// </summary>
        public static readonly int[] DefaultLandExclusions =
        {
            (int)TerrainType.Water, (int)TerrainType.CoastalSea, (int)TerrainType.OpenSea,
        };

        /// <summary>True when the position sits on a water face (for CampaignVec2.IsOnLand results).</summary>
        public bool IsWaterAt(Vec2 pos)
        {
            int ordinal = OrdinalAt(pos);
            return ordinal >= 0 && IsWater((TerrainType)Faces[ordinal].Terrain);
        }

        /// <summary>True when the face is a sea face (naval navigation layer).</summary>
        public bool IsSeaFace(int faceIndex)
            => TryGetFaceByIndex(faceIndex, out var face) && IsWater((TerrainType)face.Terrain);

        /// <summary>
        /// Cheap path-distance ESTIMATE for AI scoring: euclidean scaled by a winding factor,
        /// gated on both endpoints sharing a nav island. Campaign AI calls the distance query for
        /// dozens of candidate targets per party per think — a full A* per call stalls world
        /// generation for minutes; the native engine answers these from hierarchical acceleration
        /// structures the grid does not have. Exact paths are still computed (A*) when a party
        /// actually moves (<see cref="TryFindPath"/>).
        /// </summary>
        public bool TryEstimatePathDistance(Vec2 start, Vec2 end, int[] excludedTerrains, out float distance)
        {
            long t0 = DateTime.UtcNow.Ticks;
            try
            {
                return TryEstimatePathDistanceCore(start, end, excludedTerrains, out distance);
            }
            finally
            {
                Track(1, DateTime.UtcNow.Ticks - t0);
            }
        }

        private bool TryEstimatePathDistanceCore(Vec2 start, Vec2 end, int[] excludedTerrains, out float distance)
        {
            const float WindingFactor = 1.15f;
            distance = 0f;

            int startCell = CellOf(start);
            int endCell = CellOf(end);
            if (startCell < 0 || endCell < 0) return false;

            // A start just off the walkable layer (party parked at a settlement, or stranded on
            // water) snaps before the connectivity check so it can still score/escape.
            if (!IsCellAllowed(startCell, excludedTerrains))
            {
                if (!TryGetNearestAllowedPoint(start, excludedTerrains, CellSize * 8f, out var snapped)) return false;
                startCell = CellOf(snapped);
            }
            if (!IsCellAllowed(endCell, excludedTerrains))
            {
                // Only OFF-MESH ends (settlement-interior holes) snap. An end on EXCLUDED
                // terrain must answer unreachable like the native query: this is the AI's
                // candidate-target validator (NavigationHelper.FindReachablePointAroundPosition),
                // and snapping water points onto the shore "validated" sea positions as
                // patrol/flee targets — land parties then walked across water to reach them.
                if (_grid[endCell] >= 0) return false;
                if (!TryGetNearestAllowedPoint(end, excludedTerrains, CellSize * 8f, out var snapped)) return false;
                endCell = CellOf(snapped);
            }

            if (!AreConnected(startCell, endCell, excludedTerrains)) return false;

            distance = start.Distance(end) * WindingFactor;
            return true;
        }

        // ---- line and proximity queries ------------------------------------------------------

        /// <summary>Steps along the segment; true if every sample is an allowed cell.</summary>
        public bool IsLineClear(Vec2 from, Vec2 to, int[] excludedTerrains)
        {
            float distance = from.Distance(to);
            int steps = Math.Max(1, (int)(distance / (CellSize * 0.5f)));
            for (int i = 0; i <= steps; i++)
            {
                Vec2 p = Vec2.Lerp(from, to, i / (float)steps);
                int cell = CellOf(p);
                if (cell < 0 || !IsCellAllowed(cell, excludedTerrains)) return false;
            }
            return true;
        }

        /// <summary>Last point along the segment still on allowed cells (the point before the first blocked sample).</summary>
        public Vec2 LastClearPointOnLine(Vec2 from, Vec2 to, int[] excludedTerrains)
        {
            float distance = from.Distance(to);
            int steps = Math.Max(1, (int)(distance / (CellSize * 0.5f)));
            Vec2 last = from;
            for (int i = 0; i <= steps; i++)
            {
                Vec2 p = Vec2.Lerp(from, to, i / (float)steps);
                int cell = CellOf(p);
                if (cell < 0 || !IsCellAllowed(cell, excludedTerrains)) return last;
                last = p;
            }
            return to;
        }

        /// <summary>Nearest allowed-cell center to a position (spiral ring search).</summary>
        public bool TryGetNearestAllowedPoint(Vec2 pos, int[] excludedTerrains, float maxDistance, out Vec2 result)
        {
            long t0 = DateTime.UtcNow.Ticks;
            try
            {
                return TryGetNearestAllowedPointCore(pos, excludedTerrains, maxDistance, out result);
            }
            finally
            {
                Track(2, DateTime.UtcNow.Ticks - t0);
            }
        }

        private bool TryGetNearestAllowedPointCore(Vec2 pos, int[] excludedTerrains, float maxDistance, out Vec2 result)
        {
            int cell = CellOf(pos);
            if (cell >= 0 && IsCellAllowed(cell, excludedTerrains))
            {
                result = pos;
                return true;
            }

            int cx = Math.Min(Width - 1, Math.Max(0, (int)((pos.x - Min.x) / CellSize + 0.5f)));
            int cy = Math.Min(Height - 1, Math.Max(0, (int)((pos.y - Min.y) / CellSize + 0.5f)));
            // Unbounded searches are capped: this runs inside hot campaign loops, and anything not
            // found within 64 cells of the query point is not a useful answer anyway.
            int maxRing = maxDistance > 0 ? (int)(maxDistance / CellSize) + 1 : 64;

            for (int ring = 1; ring <= maxRing; ring++)
            {
                for (int dy = -ring; dy <= ring; dy++)
                {
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring) continue; // ring edge only
                        int gx = cx + dx, gy = cy + dy;
                        if (gx < 0 || gy < 0 || gx >= Width || gy >= Height) continue;
                        int c = gy * Width + gx;
                        if (IsCellAllowed(c, excludedTerrains))
                        {
                            result = CenterOf(c);
                            return true;
                        }
                    }
                }
            }

            result = pos;
            return false;
        }

        // ---- A* pathfinding --------------------------------------------------------------------

        /// <summary>
        /// A* between two positions over allowed cells. Fills <paramref name="waypoints"/> with a
        /// line-of-sight-smoothed point sequence (start exclusive, end inclusive) and returns the
        /// path cost in map units (with land/sea switch costs applied like the native pathfinder).
        /// </summary>
        public bool TryFindPath(
            Vec2 start, Vec2 end, int[] excludedTerrains,
            int landToSeaCost, int seaToLandCost,
            List<Vec2> waypoints, out float cost, int maxWaypoints = 64)
        {
            long t0 = DateTime.UtcNow.Ticks;
            try
            {
                return TryFindPathCore(start, end, excludedTerrains, landToSeaCost, seaToLandCost, waypoints, out cost, maxWaypoints);
            }
            finally
            {
                Track(0, DateTime.UtcNow.Ticks - t0);
            }
        }

        private static long _failOutOfBounds;
        private static long _failSnap;
        private static long _failRegion;
        private static long _failSearch;
        private static long _failLogBudget = 12;

        private static void TrackFail(ref long counter, string reason, Vec2 start, Vec2 end)
        {
            counter++;
            if (_failLogBudget > 0)
            {
                _failLogBudget--;
                Console.WriteLine($"[ServerHeadless] NavGrid path fail ({reason}): ({start.x:0.#},{start.y:0.#}) -> ({end.x:0.#},{end.y:0.#}) " +
                    $"[oob={_failOutOfBounds} snap={_failSnap} region={_failRegion} search={_failSearch}]");
            }
        }

        private bool TryFindPathCore(
            Vec2 start, Vec2 end, int[] excludedTerrains,
            int landToSeaCost, int seaToLandCost,
            List<Vec2> waypoints, out float cost, int maxWaypoints)
        {
            cost = 0f;
            int startCell = CellOf(start);
            int endCell = CellOf(end);
            if (startCell < 0 || endCell < 0)
            {
                TrackFail(ref _failOutOfBounds, "out-of-bounds", start, end);
                return false;
            }

            // Off-mesh or blocked endpoints snap to the nearest allowed cell (parties standing at
            // a settlement gate are often a hair off the sampled grid).
            if (!IsCellAllowed(startCell, excludedTerrains))
            {
                if (!TryGetNearestAllowedPoint(start, excludedTerrains, CellSize * 8f, out var snapped))
                {
                    TrackFail(ref _failSnap, "start-snap", start, end);
                    return false;
                }
                startCell = CellOf(snapped);
            }
            // The point the path actually delivers the party to. An OFF-MESH destination
            // (settlement-interior hole) stays the true target — gates legitimately sit past the
            // walkable boundary. A destination on EXCLUDED terrain (water for land movement) is
            // clamped to the snapped shore point instead: delivering the raw point walked land
            // parties onto the sea for the final hop.
            Vec2 finalPoint = end;
            if (!IsCellAllowed(endCell, excludedTerrains))
            {
                if (!TryGetNearestAllowedPoint(end, excludedTerrains, CellSize * 8f, out var snapped))
                {
                    TrackFail(ref _failSnap, "end-snap", start, end);
                    return false;
                }
                if (_grid[endCell] >= 0) finalPoint = snapped;
                endCell = CellOf(snapped);
            }
            if (startCell == endCell)
            {
                waypoints?.Add(finalPoint);
                cost = start.Distance(finalPoint);
                return true;
            }

            // Unreachable targets must fail instantly, not by exhausting the grid.
            if (!AreConnected(startCell, endCell, excludedTerrains))
            {
                TrackFail(ref _failRegion, "region", start, end);
                return false;
            }

            var s = GetScratch();
            unchecked { s.Stamp++; }
            s.OpenHeap.Clear();
            s.HeapScores.Clear();

            // Weighted A* (epsilon inflation): trades a few percent of path optimality for a
            // several-fold cut in expansions — this runs live inside campaign ticking.
            const float HeuristicWeight = 1.2f;
            const float Sqrt2 = 1.41421356f;
            int endGx = endCell % Width, endGy = endCell / Width;

            s.GScore[startCell] = 0f;
            s.CameFrom[startCell] = -1;
            s.VisitStamp[startCell] = s.Stamp;
            HeapPush(s, startCell, 0f);

            int expansions = 0;
            bool found = false;
            while (s.OpenHeap.Count > 0)
            {
                int current = HeapPop(s);
                if (current == endCell) { found = true; break; }
                if (++expansions > MaxExpansions) break;

                int cgx = current % Width, cgy = current / Width;
                bool currentWater = IsWater((TerrainType)Faces[_grid[current]].Terrain);
                float currentG = s.GScore[current];

                for (int dy = -1; dy <= 1; dy++)
                {
                    int ngy = cgy + dy;
                    if (ngy < 0 || ngy >= Height) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int ngx = cgx + dx;
                        if (ngx < 0 || ngx >= Width) continue;
                        int neighbor = ngy * Width + ngx;

                        int ordinal = _grid[neighbor];
                        if (ordinal < 0 || IsExcluded(Faces[ordinal].Terrain, excludedTerrains)) continue;

                        // No corner cutting: a diagonal step is only legal when both
                        // orthogonal neighbors are too, otherwise the movement chord clips
                        // the corner of a blocked cell (parties lerp straight between
                        // waypoints, and line-of-sight validation samples at half-cell
                        // resolution — it rightly rejects such chords).
                        if (dx != 0 && dy != 0)
                        {
                            if (!IsCellAllowed(cgy * Width + ngx, excludedTerrains) ||
                                !IsCellAllowed(ngy * Width + cgx, excludedTerrains)) continue;
                        }

                        float step = (dx != 0 && dy != 0) ? CellSize * Sqrt2 : CellSize;

                        // Land<->sea transitions cost extra, mirroring the native pathfinder's
                        // region-switch parameters (naval navigation).
                        bool neighborWater = IsWater((TerrainType)Faces[ordinal].Terrain);
                        if (!currentWater && neighborWater) step += landToSeaCost;
                        else if (currentWater && !neighborWater) step += seaToLandCost;

                        float tentative = currentG + step;
                        if (s.VisitStamp[neighbor] == s.Stamp && tentative >= s.GScore[neighbor]) continue;

                        s.GScore[neighbor] = tentative;
                        s.CameFrom[neighbor] = current;
                        s.VisitStamp[neighbor] = s.Stamp;

                        // Octile-distance heuristic in grid space — no square roots.
                        int adx = ngx > endGx ? ngx - endGx : endGx - ngx;
                        int ady = ngy > endGy ? ngy - endGy : endGy - ngy;
                        int hi = adx > ady ? adx : ady;
                        int lo = adx > ady ? ady : adx;
                        float heuristic = (hi + (Sqrt2 - 1f) * lo) * CellSize * HeuristicWeight;

                        HeapPush(s, neighbor, tentative + heuristic);
                    }
                }
            }

            if (!found) return false;

            cost = s.GScore[endCell];

            if (waypoints != null)
            {
                var corridor = new List<int>();
                for (int c = endCell; c != -1; c = s.CameFrom[c]) corridor.Add(c);
                corridor.Reverse();

                BuildWaypoints(corridor, start, finalPoint, excludedTerrains, waypoints, maxWaypoints);
            }

            return true;
        }

        /// <summary>
        /// Emits the corridor as straight segments the party can walk literally: consecutive
        /// waypoints always have line-of-sight over allowed cells (string pulling). The previous
        /// every-Nth decimation cut corners — a chord between corridor cells skirting a settlement
        /// navmesh hole crossed the hole, and parties walked straight through settlements.
        /// </summary>
        private void BuildWaypoints(List<int> corridor, Vec2 start, Vec2 end, int[] excludedTerrains, List<Vec2> waypoints, int maxWaypoints)
        {
            int budget = Math.Max(4, maxWaypoints);

            // Anchor at the party's true position when it stands on allowed cells, so the very
            // first chord is validated from where the party actually walks (positions sit off
            // cell centers). A snapped, blocked start anchors at the first corridor cell instead.
            int startCell = CellOf(start);
            Vec2 anchorPos = startCell >= 0 && IsCellAllowed(startCell, excludedTerrains)
                ? start
                : CenterOf(corridor[0]);

            int anchorIdx = 0;
            while (anchorIdx < corridor.Count - 1 && waypoints.Count < budget - 2)
            {
                // Farthest corridor cell with clear line-of-sight from the anchor: extend in big
                // jumps, halving on failure. The immediately next corridor cell is adjacent and
                // always reachable, so `reach` never gets stuck.
                int reach = anchorIdx + 1;
                int jump = 64;
                while (jump >= 1)
                {
                    int candidate = Math.Min(corridor.Count - 1, reach + jump);
                    if (candidate > reach && IsLineClear(anchorPos, CenterOf(corridor[candidate]), excludedTerrains))
                    {
                        reach = candidate;
                    }
                    else
                    {
                        jump >>= 1;
                    }
                }

                // The corridor end is visible from here; the closing chords below finish the path.
                if (reach >= corridor.Count - 1) break;

                anchorPos = CenterOf(corridor[reach]);
                waypoints.Add(anchorPos);
                anchorIdx = reach;
            }

            // Close on the true destination, which sits off the last corridor cell's center (and
            // may legitimately be inside a settlement hole — gates are targets). If the direct
            // chord from the anchor clips blocked cells, land on the last corridor cell first.
            if (!IsLineClear(anchorPos, end, excludedTerrains))
            {
                waypoints.Add(CenterOf(corridor[corridor.Count - 1]));
            }
            waypoints.Add(end);
        }

        // Binary min-heap on f-score, storing cells; f stored alongside in a parallel list.
        // Operates on the calling thread's scratch — no shared state.
        private static void HeapPush(SearchScratch s, int cell, float f)
        {
            var heap = s.OpenHeap;
            var scores = s.HeapScores;
            heap.Add(cell);
            scores.Add(f);
            int i = heap.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (scores[parent] <= scores[i]) break;
                (heap[parent], heap[i]) = (heap[i], heap[parent]);
                (scores[parent], scores[i]) = (scores[i], scores[parent]);
                i = parent;
            }
        }

        private static int HeapPop(SearchScratch s)
        {
            var heap = s.OpenHeap;
            var scores = s.HeapScores;
            int top = heap[0];
            int last = heap.Count - 1;
            heap[0] = heap[last];
            scores[0] = scores[last];
            heap.RemoveAt(last);
            scores.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int left = 2 * i + 1, right = 2 * i + 2, smallest = i;
                if (left < heap.Count && scores[left] < scores[smallest]) smallest = left;
                if (right < heap.Count && scores[right] < scores[smallest]) smallest = right;
                if (smallest == i) break;
                (heap[smallest], heap[i]) = (heap[i], heap[smallest]);
                (scores[smallest], scores[i]) = (scores[i], scores[smallest]);
                i = smallest;
            }
            return top;
        }
    }
}
