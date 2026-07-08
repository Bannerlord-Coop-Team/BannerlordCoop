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

        // A* scratch buffers, reused across queries. NOT thread-confined: the Coop mod's
        // parallel campaign tick (ParallelRobustnessPatches.ParallelTickMovingParties) issues
        // pathfinds from worker threads concurrently, so searches serialize on _searchLock —
        // racing threads desync the parallel heap lists and corrupt the search state.
        private readonly object _searchLock = new object();
        private float[] _gScore;
        private int[] _cameFrom;
        private int[] _visitStamp;
        private int _currentStamp;
        private readonly List<int> _openHeap = new List<int>();

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

            grid._gScore = new float[cells];
            grid._cameFrom = new int[cells];
            grid._visitStamp = new int[cells];
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

            // Endpoints just off the walkable layer (settlement interiors, gates) snap before the
            // connectivity check, matching how movement queries treat them.
            if (!IsCellAllowed(startCell, excludedTerrains))
            {
                if (!TryGetNearestAllowedPoint(start, excludedTerrains, CellSize * 8f, out var snapped)) return false;
                startCell = CellOf(snapped);
            }
            if (!IsCellAllowed(endCell, excludedTerrains))
            {
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
            if (!IsCellAllowed(endCell, excludedTerrains))
            {
                if (!TryGetNearestAllowedPoint(end, excludedTerrains, CellSize * 8f, out var snapped))
                {
                    TrackFail(ref _failSnap, "end-snap", start, end);
                    return false;
                }
                endCell = CellOf(snapped);
            }
            if (startCell == endCell)
            {
                waypoints?.Add(end);
                cost = start.Distance(end);
                return true;
            }

            // Unreachable targets must fail instantly, not by exhausting the grid.
            if (!AreConnected(startCell, endCell, excludedTerrains))
            {
                TrackFail(ref _failRegion, "region", start, end);
                return false;
            }

            // Everything above only reads the grid; the search and the reconstruction below use
            // the shared scratch buffers, so they run one at a time.
            lock (_searchLock)
            {
                unchecked { _currentStamp++; }
                _openHeap.Clear();
                _heapScores.Clear();

                // Weighted A* (epsilon inflation): trades a few percent of path optimality for a
                // several-fold cut in expansions — this runs live inside campaign ticking.
                const float HeuristicWeight = 1.2f;
                const float Sqrt2 = 1.41421356f;
                int endGx = endCell % Width, endGy = endCell / Width;

                _gScore[startCell] = 0f;
                _cameFrom[startCell] = -1;
                _visitStamp[startCell] = _currentStamp;
                HeapPush(startCell, 0f);

                int expansions = 0;
                bool found = false;
                while (_openHeap.Count > 0)
                {
                    int current = HeapPop();
                    if (current == endCell) { found = true; break; }
                    if (++expansions > MaxExpansions) break;

                    int cgx = current % Width, cgy = current / Width;
                    bool currentWater = IsWater((TerrainType)Faces[_grid[current]].Terrain);
                    float currentG = _gScore[current];

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

                            float step = (dx != 0 && dy != 0) ? CellSize * Sqrt2 : CellSize;

                            // Land<->sea transitions cost extra, mirroring the native pathfinder's
                            // region-switch parameters (naval navigation).
                            bool neighborWater = IsWater((TerrainType)Faces[ordinal].Terrain);
                            if (!currentWater && neighborWater) step += landToSeaCost;
                            else if (currentWater && !neighborWater) step += seaToLandCost;

                            float tentative = currentG + step;
                            if (_visitStamp[neighbor] == _currentStamp && tentative >= _gScore[neighbor]) continue;

                            _gScore[neighbor] = tentative;
                            _cameFrom[neighbor] = current;
                            _visitStamp[neighbor] = _currentStamp;

                            // Octile-distance heuristic in grid space — no square roots.
                            int adx = ngx > endGx ? ngx - endGx : endGx - ngx;
                            int ady = ngy > endGy ? ngy - endGy : endGy - ngy;
                            int hi = adx > ady ? adx : ady;
                            int lo = adx > ady ? ady : adx;
                            float heuristic = (hi + (Sqrt2 - 1f) * lo) * CellSize * HeuristicWeight;

                            HeapPush(neighbor, tentative + heuristic);
                        }
                    }
                }

                if (!found) return false;

                cost = _gScore[endCell];

                if (waypoints != null)
                {
                    // Reconstruct (end -> start), then decimate to evenly spaced waypoints. Cell-to-cell
                    // legality is already guaranteed by the search; movement between nearby waypoints
                    // stays within the corridor at this spacing, and the caller's path buffer is small.
                    var cells = new List<int>();
                    for (int c = endCell; c != -1; c = _cameFrom[c]) cells.Add(c);
                    cells.Reverse();

                    int maxPoints = Math.Max(2, maxWaypoints);
                    int step = Math.Max(1, (cells.Count + maxPoints - 1) / maxPoints);
                    for (int i = step; i < cells.Count - 1; i += step)
                    {
                        waypoints.Add(CenterOf(cells[i]));
                    }
                    waypoints.Add(end);
                }

                return true;
            }
        }

        // Binary min-heap on f-score, storing cells; f stored alongside in a parallel list.
        private readonly List<float> _heapScores = new List<float>();

        private void HeapPush(int cell, float f)
        {
            _openHeap.Add(cell);
            _heapScores.Add(f);
            int i = _openHeap.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (_heapScores[parent] <= _heapScores[i]) break;
                (_openHeap[parent], _openHeap[i]) = (_openHeap[i], _openHeap[parent]);
                (_heapScores[parent], _heapScores[i]) = (_heapScores[i], _heapScores[parent]);
                i = parent;
            }
        }

        private int HeapPop()
        {
            int top = _openHeap[0];
            int last = _openHeap.Count - 1;
            _openHeap[0] = _openHeap[last];
            _heapScores[0] = _heapScores[last];
            _openHeap.RemoveAt(last);
            _heapScores.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int left = 2 * i + 1, right = 2 * i + 2, smallest = i;
                if (left < _openHeap.Count && _heapScores[left] < _heapScores[smallest]) smallest = left;
                if (right < _openHeap.Count && _heapScores[right] < _heapScores[smallest]) smallest = right;
                if (smallest == i) break;
                (_openHeap[smallest], _openHeap[i]) = (_openHeap[i], _openHeap[smallest]);
                (_heapScores[smallest], _heapScores[i]) = (_heapScores[i], _heapScores[smallest]);
                i = smallest;
            }
            return top;
        }
    }
}
