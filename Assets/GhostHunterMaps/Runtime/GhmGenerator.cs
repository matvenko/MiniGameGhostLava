using System.Collections.Generic;
using UnityEngine;

namespace GhostHunterMaps
{
    // Turns a profile plus a level number into a finished board. Everything is a
    // pure function of (profile, level): the same inputs always produce the same
    // map, which is what lets the editor preview level 7 and the game build the
    // identical level 7 later without shipping the layout itself.
    //
    // Shape first, repair second. Each algorithm is free to carve whatever it
    // likes - the repair pass is what guarantees the result is playable: one
    // connected floor, no specks, no puddles too small to read as water, and
    // never less walkable area than the profile allows.
    public static class GhmGenerator
    {
        public static GhmBoard Generate(GhmMapProfile profile, int level)
        {
            if (profile == null) return new GhmBoard(1, 1);

            var band = profile.BandForLevel(level);
            var board = new GhmBoard(profile.width, profile.height) { level = level };
            var rng = new GhmRandom(profile.LevelSeed(level));

            float density = band != null ? band.waterDensity : 0.27f;
            var algorithm = band != null ? band.algorithm : GhmAlgorithm.ShuffleConnected;

            switch (algorithm)
            {
                case GhmAlgorithm.Caves: Caves(board, ref rng, density); break;
                case GhmAlgorithm.Rivers: Rivers(board, ref rng, density); break;
                case GhmAlgorithm.Rooms: Rooms(board, ref rng, density); break;
                case GhmAlgorithm.Archipelago: Archipelago(board, ref rng, density); break;
                default: ShuffleConnected(board, ref rng, density); break;
            }

            Repair(board, profile, band, ref rng);
            board.RebuildShoreField();

            BuildPaths(board, profile, band, ref rng);
            ScatterDecor(board, profile, band, ref rng);

            return board;
        }

        // ------------------------------------------------------------------
        // Algorithms
        // ------------------------------------------------------------------

        // What the shipped game does: start from a full floor and flip cells to
        // water one at a time, keeping a flip only when the floor stays fully
        // connected afterwards. Slow by design and impossible to break.
        private static void ShuffleConnected(GhmBoard board, ref GhmRandom rng, float density)
        {
            Fill(board, GhmCell.Ground);

            var order = new List<Vector2Int>(board.GroundCells());
            rng.Shuffle(order);

            int target = Mathf.RoundToInt(order.Count * density);
            int converted = 0;

            foreach (var c in order)
            {
                if (converted >= target) break;
                board.Set(c.x, c.y, GhmCell.Water);
                if (board.IsFullyConnected()) converted++;
                else board.Set(c.x, c.y, GhmCell.Ground);
            }
        }

        // Cellular automata: noise, then repeatedly take the majority of each
        // neighbourhood. Produces rounded lakes with soft coastlines instead of
        // the scattered single cells the shuffle leaves behind.
        private static void Caves(GhmBoard board, ref GhmRandom rng, float density)
        {
            float fill = Mathf.Clamp01(density * 1.35f);
            for (int z = 0; z < board.height; z++)
                for (int x = 0; x < board.width; x++)
                    board.Set(x, z, rng.Value < fill ? GhmCell.Water : GhmCell.Ground);

            for (int pass = 0; pass < 4; pass++)
            {
                var next = new GhmCell[board.cells.Length];
                for (int z = 0; z < board.height; z++)
                {
                    for (int x = 0; x < board.width; x++)
                    {
                        int water = 0;
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dz == 0) continue;
                                int nx = x + dx, nz = z + dz;
                                // Outside the board counts as water, which pulls
                                // the lakes towards the rim and keeps the middle
                                // of the map open.
                                if (!board.InBounds(nx, nz) || board.At(nx, nz) == GhmCell.Water) water++;
                            }
                        }
                        next[board.Index(x, z)] = water >= 5 ? GhmCell.Water : GhmCell.Ground;
                    }
                }
                board.cells = next;
            }
        }

        // Winding channels from one rim to the other, plus a lake or two where
        // they cross. This is the one that reads most like a labyrinth: long
        // barriers with a small number of crossings.
        private static void Rivers(GhmBoard board, ref GhmRandom rng, float density)
        {
            Fill(board, GhmCell.Ground);

            int cells = board.width * board.height;
            int budget = Mathf.RoundToInt(cells * density);
            int rivers = Mathf.Clamp(Mathf.RoundToInt(density * 10f), 2, 6);
            int placed = 0;

            for (int r = 0; r < rivers && placed < budget; r++)
            {
                bool horizontal = rng.Value < 0.5f;
                float t = rng.Range(0.18f, 0.82f);
                float drift = rng.Range(-0.55f, 0.55f);
                float thickness = rng.Range(0.9f, 1.9f);
                int steps = horizontal ? board.width : board.height;
                float pos = horizontal ? t * board.height : t * board.width;

                for (int s = 0; s < steps && placed < budget; s++)
                {
                    pos += drift * 0.35f + (GhmNoise.Smooth(s * 0.22f + r * 13.7f, r * 5.1f) - 0.5f) * 1.4f;
                    drift += (rng.Value - 0.5f) * 0.35f;
                    drift = Mathf.Clamp(drift, -0.8f, 0.8f);

                    int half = Mathf.Max(0, Mathf.RoundToInt(thickness * 0.5f));
                    for (int w = -half; w <= half; w++)
                    {
                        int x = horizontal ? s : Mathf.RoundToInt(pos) + w;
                        int z = horizontal ? Mathf.RoundToInt(pos) + w : s;
                        if (!board.InBounds(x, z) || board.At(x, z) == GhmCell.Water) continue;
                        board.Set(x, z, GhmCell.Water);
                        placed++;
                    }
                }
            }

            // Lakes soak up whatever budget the channels left, so the density
            // slider still means what it says.
            int guard = 0;
            while (placed < budget && guard++ < 400)
            {
                int cx = rng.Range(1, board.width - 1);
                int cz = rng.Range(1, board.height - 1);
                int radius = rng.Range(1, 3);
                for (int dz = -radius; dz <= radius; dz++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (dx * dx + dz * dz > radius * radius) continue;
                        int x = cx + dx, z = cz + dz;
                        if (!board.InBounds(x, z) || board.At(x, z) == GhmCell.Water) continue;
                        board.Set(x, z, GhmCell.Water);
                        placed++;
                    }
                }
            }
        }

        // Rectangular water blocks on a loose lattice, leaving a corridor grid
        // between them - the most "designed" looking of the five.
        private static void Rooms(GhmBoard board, ref GhmRandom rng, float density)
        {
            Fill(board, GhmCell.Ground);

            int budget = Mathf.RoundToInt(board.width * board.height * density);
            int placed = 0;
            int guard = 0;

            while (placed < budget && guard++ < 300)
            {
                int w = rng.Range(2, Mathf.Max(3, board.width / 3));
                int h = rng.Range(2, Mathf.Max(3, board.height / 3));
                int x0 = rng.Range(1, Mathf.Max(2, board.width - w - 1));
                int z0 = rng.Range(1, Mathf.Max(2, board.height - h - 1));

                // Blocks are kept one cell apart so the corridors between them
                // never close up into a single mass of water.
                bool clear = true;
                for (int z = z0 - 1; z <= z0 + h && clear; z++)
                    for (int x = x0 - 1; x <= x0 + w && clear; x++)
                        if (board.At(x, z) == GhmCell.Water) clear = false;
                if (!clear) continue;

                for (int z = z0; z < z0 + h; z++)
                {
                    for (int x = x0; x < x0 + w; x++)
                    {
                        if (!board.InBounds(x, z)) continue;
                        board.Set(x, z, GhmCell.Water);
                        placed++;
                    }
                }
            }
        }

        // Threshold a smooth noise field: broad water with islands in it, the
        // inverse of the cave shape.
        private static void Archipelago(GhmBoard board, ref GhmRandom rng, float density)
        {
            float ox = rng.Range(0f, 128f);
            float oz = rng.Range(0f, 128f);
            var values = new float[board.cells.Length];

            for (int z = 0; z < board.height; z++)
            {
                for (int x = 0; x < board.width; x++)
                {
                    float n = GhmNoise.Fbm(x * 0.28f + ox, z * 0.28f + oz);
                    // Pull the value down towards the rim so the board ends in
                    // water rather than being cut off mid-island by the wall.
                    float edge = Mathf.Min(Mathf.Min(x, board.width - 1 - x), Mathf.Min(z, board.height - 1 - z));
                    n -= Mathf.Clamp01(1f - edge / 2.2f) * 0.28f;
                    values[board.Index(x, z)] = n;
                }
            }

            var sorted = new List<float>(values);
            sorted.Sort();
            int cut = Mathf.Clamp(Mathf.RoundToInt(sorted.Count * density), 0, sorted.Count - 1);
            float threshold = sorted[cut];

            for (int i = 0; i < values.Length; i++)
                board.cells[i] = values[i] <= threshold ? GhmCell.Water : GhmCell.Ground;
        }

        private static void Fill(GhmBoard board, GhmCell cell)
        {
            for (int i = 0; i < board.cells.Length; i++) board.cells[i] = cell;
        }

        // ------------------------------------------------------------------
        // Repair
        // ------------------------------------------------------------------

        // The contract every algorithm above is allowed to ignore. Runs in a
        // fixed order: reconnect what is worth keeping, drop what is not, top the
        // floor back up if the carve went too far, then tidy the water.
        private static void Repair(GhmBoard board, GhmMapProfile profile, GhmLevelBand band, ref GhmRandom rng)
        {
            ConnectRegions(board, profile.minIslandSize);
            EnforceWalkableShare(board, profile.minWalkableShare);
            RemoveSmallPools(board, band != null ? band.minPoolSize : 2);

            // Cheap last resort: if something above still left the floor split,
            // flood the offending regions rather than shipping an impossible map.
            if (!board.IsFullyConnected()) ConnectRegions(board, 1);
            if (!board.IsFullyConnected()) KeepLargestRegionOnly(board);
        }

        // Islands worth keeping get a one-cell causeway carved to the mainland;
        // anything under the threshold is simply flooded. Carving rather than
        // deleting is what keeps the interesting shapes an algorithm found.
        private static void ConnectRegions(GhmBoard board, int minIslandSize)
        {
            var regions = board.Regions(GhmCell.Ground);
            if (regions.Count <= 1) return;

            regions.Sort((a, b) => b.Count.CompareTo(a.Count));
            var main = new HashSet<Vector2Int>(regions[0]);

            for (int r = 1; r < regions.Count; r++)
            {
                var region = regions[r];
                if (region.Count < minIslandSize)
                {
                    foreach (var c in region) board.Set(c.x, c.y, GhmCell.Water);
                    continue;
                }

                CarveBridge(board, region, main);
                foreach (var c in region) main.Add(c);
            }
        }

        // Shortest hop between the island and the mainland, then every cell on
        // the straight line between those two is turned into floor.
        private static void CarveBridge(GhmBoard board, List<Vector2Int> region, HashSet<Vector2Int> main)
        {
            float best = float.MaxValue;
            Vector2Int from = region[0], to = from;

            foreach (var a in region)
            {
                foreach (var b in main)
                {
                    float d = (a - b).sqrMagnitude;
                    if (d >= best) continue;
                    best = d; from = a; to = b;
                }
            }

            int x = from.x, z = from.y;
            int guard = 0;
            while ((x != to.x || z != to.y) && guard++ < 512)
            {
                if (x != to.x) x += x < to.x ? 1 : -1;
                else if (z != to.y) z += z < to.y ? 1 : -1;
                board.Set(x, z, GhmCell.Ground);
                main.Add(new Vector2Int(x, z));
            }
        }

        private static void KeepLargestRegionOnly(GhmBoard board)
        {
            var regions = board.Regions(GhmCell.Ground);
            if (regions.Count <= 1) return;
            regions.Sort((a, b) => b.Count.CompareTo(a.Count));
            for (int r = 1; r < regions.Count; r++)
                foreach (var c in regions[r]) board.Set(c.x, c.y, GhmCell.Water);
        }

        // An aggressive density plus an unlucky seed can leave almost nothing to
        // walk on. Water cells are reclaimed most-neighbours-first, so the floor
        // grows back along the existing coast instead of punching holes in lakes.
        private static void EnforceWalkableShare(GhmBoard board, float minShare)
        {
            int total = board.cells.Length;
            int wanted = Mathf.RoundToInt(total * Mathf.Clamp01(minShare));
            int guard = 0;

            while (board.GroundCount < wanted && guard++ < total * 2)
            {
                Vector2Int best = new Vector2Int(-1, -1);
                int bestScore = -1;

                for (int z = 0; z < board.height; z++)
                {
                    for (int x = 0; x < board.width; x++)
                    {
                        if (board.At(x, z) != GhmCell.Water) continue;
                        int score = 0;
                        for (int d = 0; d < GhmBoard.Dirs.Length; d++)
                            if (board.IsGround(x + GhmBoard.Dirs[d].x, z + GhmBoard.Dirs[d].y)) score++;
                        if (score <= bestScore) continue;
                        bestScore = score; best = new Vector2Int(x, z);
                    }
                }

                if (best.x < 0 || bestScore <= 0) break;
                board.Set(best.x, best.y, GhmCell.Ground);
            }
        }

        // A one-cell puddle is a hole in the floor, not a pond: the merged
        // liquid surface has nothing to fade across and it just looks broken.
        private static void RemoveSmallPools(GhmBoard board, int minPoolSize)
        {
            if (minPoolSize <= 1) return;
            foreach (var region in board.Regions(GhmCell.Water))
            {
                if (region.Count >= minPoolSize) continue;
                foreach (var c in region) board.Set(c.x, c.y, GhmCell.Ground);
            }
        }

        // ------------------------------------------------------------------
        // Paths
        // ------------------------------------------------------------------

        // Routes between spread-out anchors, laid down one after another. Each
        // route is allowed to see the ones before it and prefers to merge into
        // them, which is what turns a handful of lines into a network with
        // junctions rather than parallel stripes.
        private static void BuildPaths(GhmBoard board, GhmMapProfile profile, GhmLevelBand band, ref GhmRandom rng)
        {
            if (band != null && !band.drawPaths) return;

            var ground = board.GroundCells();
            if (ground.Count < 4) return;

            foreach (var layer in profile.LayersOfKind(GhmLayerKind.Path, board.level))
            {
                var settings = layer.path;
                if (settings.anchors < 2) continue;

                var anchors = PickAnchors(board, ground, settings.anchors, ref rng);
                var onPath = new HashSet<Vector2Int>();

                int legs = settings.closeLoop ? anchors.Count : anchors.Count - 1;
                for (int i = 0; i < legs; i++)
                {
                    var a = anchors[i];
                    var b = anchors[(i + 1) % anchors.Count];
                    var route = RouteBetween(board, a, b, onPath, settings, rng.Range(0, 100000));
                    foreach (var c in route) onPath.Add(c);
                }

                foreach (var c in onPath)
                {
                    if (!board.pathRoute.Contains(c)) board.pathRoute.Add(c);
                }
            }

            StampPathField(board, profile);
        }

        // Farthest-point sampling: each new anchor is the ground cell furthest
        // from every anchor already chosen, so four anchors land in four corners
        // of the walkable area instead of clumping wherever the RNG went.
        private static List<Vector2Int> PickAnchors(GhmBoard board, List<Vector2Int> ground, int count, ref GhmRandom rng)
        {
            var anchors = new List<Vector2Int> { ground[rng.Range(0, ground.Count)] };

            while (anchors.Count < Mathf.Min(count, ground.Count))
            {
                Vector2Int best = anchors[0];
                float bestDist = -1f;
                foreach (var c in ground)
                {
                    float nearest = float.MaxValue;
                    foreach (var a in anchors) nearest = Mathf.Min(nearest, (c - a).sqrMagnitude);
                    if (nearest <= bestDist) continue;
                    bestDist = nearest; best = c;
                }
                anchors.Add(best);
            }
            return anchors;
        }

        // Dijkstra over the floor with a shaped cost: reuse makes cells that are
        // already path nearly free, shoreAvoidance taxes cells close to the
        // water, and wander adds a smooth noise field so the result curves
        // instead of running down a perfectly straight staircase.
        private static List<Vector2Int> RouteBetween(GhmBoard board, Vector2Int from, Vector2Int to,
            HashSet<Vector2Int> existing, GhmPathSettings settings, int noiseSalt)
        {
            var dist = new Dictionary<Vector2Int, float> { [from] = 0f };
            var prev = new Dictionary<Vector2Int, Vector2Int>();
            var open = new List<Vector2Int> { from };
            var closed = new HashSet<Vector2Int>();

            while (open.Count > 0)
            {
                int bestIndex = 0;
                for (int i = 1; i < open.Count; i++)
                    if (dist[open[i]] < dist[open[bestIndex]]) bestIndex = i;

                var current = open[bestIndex];
                open.RemoveAt(bestIndex);
                if (current == to) break;
                if (!closed.Add(current)) continue;

                for (int d = 0; d < GhmBoard.Dirs.Length; d++)
                {
                    var n = current + GhmBoard.Dirs[d];
                    if (!board.IsGround(n) || closed.Contains(n)) continue;

                    float cost = 1f;
                    if (existing.Contains(n)) cost *= Mathf.Lerp(1f, 0.15f, settings.reuse);
                    float shore = board.ShoreAt(n.x, n.y);
                    if (shore < settings.shoreAvoidance) cost += (settings.shoreAvoidance - shore) * 1.5f;
                    cost += GhmNoise.Smooth(n.x * 0.35f + noiseSalt * 0.013f, n.y * 0.35f) * settings.wander * 2.4f;

                    float candidate = dist[current] + cost;
                    if (dist.TryGetValue(n, out float known) && known <= candidate) continue;
                    dist[n] = candidate;
                    prev[n] = current;
                    if (!open.Contains(n)) open.Add(n);
                }
            }

            var route = new List<Vector2Int>();
            if (!dist.ContainsKey(to)) return route;

            var step = to;
            int guard = 0;
            while (step != from && guard++ < 4096)
            {
                route.Add(step);
                if (!prev.TryGetValue(step, out step)) return route;
            }
            route.Add(from);

            if (settings.smooth) SmoothCorners(board, route);
            return route;
        }

        // A grid route turns in right angles; adding the inner cell of each
        // corner rounds it off so the painted path does not read as pixel steps.
        private static void SmoothCorners(GhmBoard board, List<Vector2Int> route)
        {
            var extra = new List<Vector2Int>();
            for (int i = 1; i < route.Count - 1; i++)
            {
                var a = route[i - 1];
                var b = route[i + 1];
                if (a.x == b.x || a.y == b.y) continue;
                var corner = new Vector2Int(a.x, b.y);
                if (corner == route[i]) corner = new Vector2Int(b.x, a.y);
                if (board.IsGround(corner)) extra.Add(corner);
            }
            route.AddRange(extra);
        }

        // Per-cell path strength, used by the decor masks and as the coarse
        // version of what the path mesh paints at corner resolution.
        private static void StampPathField(GhmBoard board, GhmMapProfile profile)
        {
            for (int i = 0; i < board.path.Length; i++) board.path[i] = 0f;
            if (board.pathRoute.Count == 0) return;

            var layer = profile.FirstLayer(GhmLayerKind.Path);
            float width = layer != null ? layer.path.width : 1f;
            float softness = layer != null ? layer.path.edgeSoftness : 0.5f;

            var field = new GhmPathField(board.pathRoute, width, softness);
            for (int z = 0; z < board.height; z++)
                for (int x = 0; x < board.width; x++)
                    board.path[board.Index(x, z)] = field.Sample(x, z);
        }

        // ------------------------------------------------------------------
        // Decor
        // ------------------------------------------------------------------

        // Rejection sampling with a spacing grid. Deliberately not a per-cell
        // dice roll: things that grow (flowers, reeds) come in clumps, and a
        // uniform per-cell chance produces an even dusting that reads as noise.
        private static void ScatterDecor(GhmBoard board, GhmMapProfile profile, GhmLevelBand band, ref GhmRandom rng)
        {
            var ground = board.GroundCells();
            if (ground.Count == 0) return;

            float densityScale = band != null ? band.decorDensityScale : 1f;

            for (int li = 0; li < profile.layers.Count; li++)
            {
                var layer = profile.layers[li];
                if (layer.kind != GhmLayerKind.Decor || !layer.ActiveAtLevel(board.level)) continue;

                for (int ri = 0; ri < layer.rules.Count; ri++)
                {
                    var rule = layer.rules[ri];
                    if (!rule.enabled) continue;
                    if (board.level < rule.minLevel || board.level > rule.maxLevel) continue;
                    if (rule.source == GhmDecorSource.Texture && rule.texture == null && rule.materialOverride == null) continue;
                    if (rule.source == GhmDecorSource.Prefab && rule.prefab == null) continue;

                    int target = Mathf.RoundToInt(ground.Count / 100f * rule.per100Cells * densityScale);
                    if (target <= 0) continue;

                    var ruleRng = new GhmRandom(profile.LevelSeed(board.level) + rule.seedSalt * 6151 + li * 131 + ri * 17);
                    var occupied = new Dictionary<Vector2Int, List<Vector2>>();
                    int placed = 0;
                    int attempts = 0;
                    int maxAttempts = Mathf.Max(600, target * 40);

                    while (placed < target && attempts++ < maxAttempts)
                    {
                        var cell = ground[ruleRng.Range(0, ground.Count)];
                        if (!PlacementAllows(board, rule, cell)) continue;

                        int cluster = Mathf.Max(1, rule.clusterSize);
                        for (int k = 0; k < cluster && placed < target; k++)
                        {
                            Vector2 local = new Vector2(
                                (ruleRng.Value - 0.5f) * 2f * rule.positionJitter,
                                (ruleRng.Value - 0.5f) * 2f * rule.positionJitter);
                            if (k > 0)
                            {
                                float a = ruleRng.Value * Mathf.PI * 2f;
                                float r = ruleRng.Value * rule.clusterRadius;
                                local += new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                            }

                            var point = new Vector2(cell.x + local.x, cell.y + local.y);
                            var pointCell = new Vector2Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y));
                            if (!board.IsGround(pointCell)) continue;
                            if (!SpacingAllows(occupied, point, rule.minSpacing)) continue;

                            Remember(occupied, point);

                            Vector3 world = profile.CellToWorld(pointCell.x, pointCell.y, false);
                            world.x += (point.x - pointCell.x) * profile.cellSize;
                            world.z += (point.y - pointCell.y) * profile.cellSize;
                            world.y += rule.yOffset;

                            float shade = 1f + (ruleRng.Value - 0.5f) * 2f * rule.tintVariation;
                            var tint = rule.tint * new Color(shade, shade, shade, 1f);
                            tint.a = rule.tint.a;

                            board.decor.Add(new GhmDecorPlacement
                            {
                                layerIndex = li,
                                ruleIndex = ri,
                                position = world,
                                yaw = (ruleRng.Value - 0.5f) * 2f * rule.yawJitter,
                                scale = rule.baseScale * ruleRng.Range(rule.scaleRange.x, rule.scaleRange.y),
                                tint = tint
                            });
                            placed++;
                        }
                    }
                }
            }
        }

        private static bool PlacementAllows(GhmBoard board, GhmDecorRule rule, Vector2Int cell)
        {
            if (rule.placement == GhmPlacement.Anywhere) return true;

            float shore = board.ShoreAt(cell.x, cell.y);
            float path = board.PathAt(cell.x, cell.y);

            if ((rule.placement & GhmPlacement.Inland) != 0 && shore < rule.inlandMargin) return false;
            if ((rule.placement & GhmPlacement.Shore) != 0 && shore > rule.shoreBand) return false;
            if ((rule.placement & GhmPlacement.OnPath) != 0 && path < 0.35f) return false;
            if ((rule.placement & GhmPlacement.OffPath) != 0 && path > 0.12f) return false;
            if ((rule.placement & GhmPlacement.PathEdge) != 0 && (path < 0.08f || path > 0.6f)) return false;

            if ((rule.placement & GhmPlacement.Corner) != 0)
            {
                int open = 0;
                for (int d = 0; d < GhmBoard.Dirs.Length; d++)
                    if (board.IsGround(cell + GhmBoard.Dirs[d])) open++;
                if (open > 2) return false;
            }

            return true;
        }

        private static bool SpacingAllows(Dictionary<Vector2Int, List<Vector2>> occupied, Vector2 point, float spacing)
        {
            if (spacing <= 0f) return true;
            float sq = spacing * spacing;
            int reach = Mathf.CeilToInt(spacing);
            var home = new Vector2Int(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));

            for (int dz = -reach; dz <= reach; dz++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    if (!occupied.TryGetValue(new Vector2Int(home.x + dx, home.y + dz), out var bucket)) continue;
                    for (int i = 0; i < bucket.Count; i++)
                        if ((bucket[i] - point).sqrMagnitude < sq) return false;
                }
            }
            return true;
        }

        private static void Remember(Dictionary<Vector2Int, List<Vector2>> occupied, Vector2 point)
        {
            var home = new Vector2Int(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));
            if (!occupied.TryGetValue(home, out var bucket))
            {
                bucket = new List<Vector2>();
                occupied[home] = bucket;
            }
            bucket.Add(point);
        }
    }

    // Distance to the nearest route cell, bucketed so the path mesh can sample
    // it per vertex without walking the whole route every time.
    public class GhmPathField
    {
        private readonly Dictionary<Vector2Int, List<Vector2Int>> _buckets = new Dictionary<Vector2Int, List<Vector2Int>>();
        private readonly float _halfWidth;
        private readonly float _falloff;
        private readonly int _reach;

        public GhmPathField(List<Vector2Int> route, float width, float softness)
        {
            _halfWidth = Mathf.Max(0.15f, width * 0.5f);
            _falloff = Mathf.Max(0.08f, softness * _halfWidth + 0.12f);
            _reach = Mathf.CeilToInt(_halfWidth + _falloff) + 1;

            foreach (var c in route)
            {
                if (!_buckets.TryGetValue(c, out var bucket))
                {
                    bucket = new List<Vector2Int>();
                    _buckets[c] = bucket;
                }
                bucket.Add(c);
            }
        }

        public bool IsEmpty => _buckets.Count == 0;

        public float Sample(float x, float z)
        {
            if (_buckets.Count == 0) return 0f;

            float nearest = float.MaxValue;
            int cx = Mathf.RoundToInt(x), cz = Mathf.RoundToInt(z);

            for (int dz = -_reach; dz <= _reach; dz++)
            {
                for (int dx = -_reach; dx <= _reach; dx++)
                {
                    if (!_buckets.ContainsKey(new Vector2Int(cx + dx, cz + dz))) continue;
                    float ddx = cx + dx - x, ddz = cz + dz - z;
                    nearest = Mathf.Min(nearest, ddx * ddx + ddz * ddz);
                }
            }

            if (nearest == float.MaxValue) return 0f;
            float d = Mathf.Sqrt(nearest);
            return Mathf.Clamp01((_halfWidth + _falloff - d) / _falloff);
        }
    }
}
